# Design: jira-ado-sync

## 1. Goals

- Bidirectional sync of work items between Jira Cloud and Azure DevOps (ADO).
- Generic and configurable. No organization, project, or path is hard-coded.
- Loop-safe: an update that the sync itself caused must not trigger another update.
- Deployable as an Azure Function (reference deployment).

## 2. Non-goals (v1)

- Jira Data Center / Server.
- Attachments, custom field mapping, boards, sprints as objects (only iteration assignment is in scope).
- Real-time conflict merge. v1 uses last-write-wins.

## 3. Architecture

```
            ┌────────────────┐   webhook (issue created/updated,
            │  Jira Cloud    │   comment created, issuelink created)
            └──────┬─▲───────┘
          webhook  │ │ REST API v3 (writes)
                   ▼ │
          ┌──────────────────────┐        ┌──────────────────────────┐
          │ Azure Function       │ ◄────► │ Azure Table Storage      │
          │ (HTTP triggers)      │        │ ID mapping + sync state  │
          └──────┬─▲─────────────┘        └──────────────────────────┘
   REST (writes)  │ │ service hook (work item created/updated)
                  ▼ │
          ┌────────────────┐
          │ Azure DevOps   │
          └────────────────┘
```

Components:

- **Jira webhook receiver:** HTTP-triggered function. Authenticates the request, stores the raw delivery in Blob Storage
  and enqueues it, then answers `202` (ADR 0004; the other codes are in ADR 0007). Changes arrive by webhook, not by polling (ADR 0003).
- **ADO service hook receiver:** HTTP-triggered function, same pattern.
- **Processor:** queue-triggered function. Reads the stored delivery, parses it and runs the sync processor. A failure
  that can succeed later is thrown, so the queue retries it (ADR 0004).
- **Sync engine:** pure logic that maps a source change to a target change using the config. It has no direct dependency on either API, which makes it testable.
- **Adapters:** `JiraClient` and `AdoClient`, thin REST wrappers.
- **State store:** Azure Table Storage. One row per synced item.

## 4. Data model

Table `SyncItems`, one row per linked pair:

| Field | Meaning |
|---|---|
| `PartitionKey` | `projectMappingId` from the config |
| `RowKey` | Jira issue key, e.g. `PROJ-123` |
| `AdoWorkItemId` | ADO work item ID |
| `LastJiraUpdated` | Jira `updated` timestamp last written or seen |
| `LastAdoRev` | ADO `rev` last written or seen |
| `LastWriterSystem` | `jira` or `ado`, the side that last wrote |
| `LastSyncedHash` | Hash of the synced field set at last sync (see loop prevention) |

## 5. Sync flow

1. Event arrives from one side (source).
2. Look up the pair in `SyncItems`. If none exists and the mapping allows creation, create the item on the other side and store the pair.
3. Compare the event's changed fields with `LastSyncedHash`. If nothing in the synced field set changed, stop.
4. Apply the status and field mapping from the config to the target.
5. Write to the target and record the new `LastSyncedHash`, `LastWriterSystem`, and version.

## 6. Loop prevention

Two safeguards, both required:

1. **Echo suppression via the sync identity.** Writes made by this function use a dedicated service account (Jira) and a dedicated identity (ADO). Events whose actor is that identity are dropped.
2. **Hash check.** If the resulting synced field set equals `LastSyncedHash`, no write is issued. This catches races and duplicate deliveries.

Webhooks can be delivered more than once, so processing is idempotent by design.

## 7. Conflict policy

- **v1 `lastWriteWins`:** the change with the newer timestamp wins. Jira uses `updated`, ADO uses `ChangedDate`. Clock skew is handled by comparing against the last synced value, not wall-clock time.
- **Planned `sourceOfTruthPerField`:** a field is owned by one side (e.g. status from ADO, description from Jira).
- **Planned `flagForReview`:** if both sides changed since last sync, add a comment on both items and skip the write.

## 8. Mapping configuration

See `config/config.example.json`. Key points:

- `projectMappings` defines which Jira project maps to which ADO project and area path.
- `statusMap` maps Jira status names to ADO states. Workflows differ between teams, so this is always explicit. Unmapped statuses are logged and not synced.
- `linkTypeMap` maps Jira link types to ADO link types. Unmapped links are skipped.
- `features` toggles each synced field.

## 9. Secrets

Secrets never live in the config file.

- **Jira:** API token of a dedicated service account. Stored as an app setting or in Key Vault.
- **ADO:** Managed Identity of the Function, granted access to the ADO organization. A PAT is supported for local testing only.
- **Webhook authentication:** per source. Jira signs deliveries of admin webhooks that have a secret with an HMAC in the
  `X-Hub-Signature` header. ADO sends basic authentication, which must use HTTPS. Requests without a valid credential are
  rejected. The exact checks are in ADR 0006.

## 10. Testing

- Unit tests for the sync engine (mapping, hashing, conflict logic). These use no network.
- Contract tests for the adapters against recorded responses.
- A sandbox mapping with two throwaway projects for end-to-end runs.

## 11. Open questions

- Whether Jira webhooks should be registered per project or globally (depends on the admin rights of the user installing it).
- Whether iteration sync should map Jira sprints to ADO iterations by name or by a config table.
- Retry strategy: decided in ADR 0005. Open: the status code of a failed `/rev` test, which needs a real response.
