# Roadmap

jira-ado-sync keeps Jira Cloud issues and Azure DevOps work items in step, in both directions, as an Azure Function.
The design is in [DESIGN.md](DESIGN.md); this file plans the way there. Status: 14 November 2026.

Legend: `[ ]` open · `[~]` in progress · `[x]` done · `[!]` blocked

## Guardrails

- **Walking skeleton before breadth.** Title, description and status run in both directions, end to end against real
  systems, before a new field or feature is added.
- **The core stays pure.** HTTP, storage, clock and configuration sources live in the host and adapter projects behind
  interfaces that `JiraAdoSync.Core` defines. Third-party and Azure SDK types do not reach the core.
- **Nothing is ignored silently.** A setting the sync cannot honour yet is not in the schema or the loader. Comments,
  links and iterations were removed from the configuration for that reason and come back with their implementation.
- **Real payloads beat documentation.** Where the vendor documentation is ambiguous, a recorded delivery becomes a test
  fixture and decides.
- Every phase ends with something that runs and can be shown.

## Releases

| Release | Content |
| --- | --- |
| **0.1.0** | Phases 1 and 2: title, description and status in both directions, deployed to Azure, proven in a sandbox |
| 0.x | Phase 3: creating items, assignee, comments, links, iterations |
| 1.0 | Phase 4 (conflict policies), plus everything above running against real projects without manual repair |

## Phase 0: foundation (done)

- [x] Design document, MIT licence, example configuration
- [x] Pure sync engine: echo suppression for the sync's own identity, hash check over the synced field set, status
  mapping in both directions, last write wins
- [x] JSON configuration with JSON Schema (Draft 2020-12) and loader: unknown properties are rejected, all errors are
  reported at once, the shipped example is tested; secrets are referenced by setting name only
- [x] Webhook parsers for `jira:issue_created`/`updated` and `workitem.created`/`updated`, request builders (Jira issue
  update, transition lookup and transition, ADO JSON Patch with a `test` on `/rev`), ADF and HTML to plain text
- [x] Loop freedom proven in the core: a Jira change is written to ADO, and its echo from ADO is `NoChange`, also with
  a different HTML line break form
- [x] engineering-baseline adopted: C# build rules on .NET 10, pipeline, project files
  ([#1](https://github.com/miralem-cebic/jira-ado-sync/pull/1)); CI and Dependabot green
- [x] Repository settings applied with `scripts/apply-repo-settings.sh`: `main` is protected, pull requests are
  squash-merged and their branches deleted on merge (seen on
  [#2](https://github.com/miralem-cebic/jira-ado-sync/pull/2))
- [x] Private vulnerability reporting is enabled; `SECURITY.md` points to it

Moved out of this phase, because they are not foundation work: the schema check (Phase 1) and the conformance pin
(Maintenance, done with engineering-baseline v0.2.0).

## Phase 1: walking skeleton

Goal: a change of title, description or status travels Jira → ADO and ADO → Jira through the Function, and its echo is
not written back.

- [x] Decision (ADR): acknowledge and enqueue deliveries ([ADR 0004](adr/0004-acknowledge-and-enqueue-deliveries.md)),
  with webhooks as the transport ([ADR 0003](adr/0003-receive-changes-by-webhook.md)). The vendors' retry rules decide
  it: ADO retries only 408, 502, 503 and 504, and any other error puts the subscription on probation
- [x] Interfaces in the core: `IJiraClient`, `IAdoClient` and `ISyncStateStore` (save with ETag), used by
  `SyncProcessor`, which is tested against fakes. The processor needs no clock, so `TimeProvider` comes in with the host
  that needs one
- [x] Schema test: `ConfigSchemaTests` checks that the example, the schema and the loader agree on 14 cases, with
  NJsonSchema as a test-only dependency (`docs/dependencies.md`). It found three gaps: the loader accepted an empty ADO
  state, and the schema accepted whitespace and an `https://` URL without a host. Both sides now agree. Webhook settings
  get their own cases when they are added
- [~] Jira REST client (API v3) in `JiraAdoSync.Adapters`: read the issue status, update summary and description, list
  transitions and transition. The host sets the e-mail and the API token with `BasicAuthorizationHandler`, and the retry
  policy in front. Recorded responses come in Phase 2
- [~] ADO REST client (7.1) in `JiraAdoSync.Adapters`: read the revision, JSON Patch with the `/rev` test. The host sets
  the managed identity (`BearerAuthorizationHandler`) or a PAT for local testing
- [~] Retry strategy: decided in ADR 0005 and implemented in `RetryingHandler`. Open: the status code of a failed `/rev`
  test, which needs a real response. The clients throw on any failure, so the queue reads the item again and decides
  again (ADR 0004)
- [~] Contract tests for both clients against the documented response shapes (DESIGN.md, section 10). Recorded responses
  replace them in Phase 2
- [x] State store on Azure Table Storage (`TableSyncStateStore`): table `SyncItems` as in DESIGN.md section 4. The save
  uses the entity ETag, so two concurrent events for one pair cannot both win. Tested against Azurite
- [x] Receiver and processor behind ports (ADR 0004, ADR 0007): `WebhookReceiver` authenticates, checks that the body is
  JSON, stores it under `source/SHA-256` and enqueues a message that names it. `DeliveryProcessor` loads it, parses it,
  maps the project and runs the sync. A redelivery keeps one stored body. The Azure adapters for the ports come with the
  host
- [x] Azure Functions host (isolated worker, .NET 10) with HTTP triggers for the Jira webhook and the ADO service hook,
  and a queue trigger for the processor (ADR 0004, ADR 0008). It starts and answers locally against Azurite
  (`docs/running-locally.md`). Deployment is Phase 2
- [x] Webhook authentication: decided in ADR 0006, checked before anything is parsed or stored (`WebhookAuthenticator`,
  `WebhookReceiver`). The host answers `401` and maps every failure to `503`
- [x] Security headers on every response: `ResponseHeaders` sets the four headers, and a local request shows them (`curl
  -i`). They match the API rules HOS-009..012
- [x] `CA2007` decided for the host project: it stays on and every `await` uses `ConfigureAwait(false)` (ADR 0008)
- [x] One log line when a delivery is answered and one when it is processed. Each carries source, item key, blob name
  and decision. Bodies, headers and secrets are never logged
- [x] New packages in `docs/dependencies.md`, with the licence of each (MIT, Apache-2.0 and BSD-3-Clause)

**Acceptance:** the same event delivered twice writes once; an older event never overwrites a newer change; the echo of
the sync's own write ends as `EchoSuppressed` or `NoChange`. The core tests prove all three (`SyncProcessorTests`); the sandbox run is Phase 2. The host runs locally against Azurite (`docs/running-locally.md`).

## Phase 2: real systems

- [!] Sandbox: one throwaway Jira project, one throwaway ADO project and a mapping between them. Blocked until the Jira
  site URL and the ADO organization and projects are known; needed only for tests against real systems
- [ ] Record one real delivery per event type (Jira webhook, ADO service hook) and keep it as a fixture without personal
  data. Confirm the `X-Hub-Signature` format of Jira (ADR 0006), and whether `resource.revision.fields` carries the full
  state. The ADO parser is defensive until then
- [ ] Decision: register the Jira webhook per project or globally (DESIGN.md section 11; depends on the admin rights of
  the person installing it)
- [ ] Webhook operations: a timer function (or a runbook) refreshes the REST-registered Jira webhooks, which expire
  after 30 days. Someone checks the ADO Service Hooks page for subscriptions on probation or disabled (ADR 0003)
- [ ] Deployment example as infrastructure as code: Function app, storage account, Key Vault, managed identity with
  access to the storage and the ADO organization, a lifecycle rule that deletes raw deliveries after seven days (ADR
  0004), and an alert on the poison queue. Documented in `docs/`
- [ ] Repeatable end-to-end run in the sandbox for both directions, the echo case and a duplicate delivery
- [ ] SBOM for the released Function package (baseline rule SEC-002)
- [ ] Release 0.1.0

## Phase 3: breadth

Each item brings its configuration, schema and loader validation along; nothing is configured before it works.

- [ ] Create the item on the other side when no pair exists: issue type and work item type per mapping, area path from
  the mapping, the new pair stored
- [ ] Assignee: a user mapping from Jira `accountId` to ADO identity in the configuration. Until then the parsers
  deliver no assignee
- [ ] Comments in both directions (DESIGN.md lists `comment_created`), echo-safe like the fields
- [ ] Links: `linkTypeMap` from Jira link types to ADO link types; unmapped links are skipped (DESIGN.md section 8)
- [ ] Iterations: Jira sprint to ADO iteration. Decision: by name or by a table in the configuration (DESIGN.md
  section 11)

- - [ ] Status changes that need several Jira transitions: today a status without a direct transition from the current
  state throws, so the event is retried and ends in the poison queue

## Phase 4: conflict policies

DESIGN.md section 7. v1 is `lastWriteWins`.

- [ ] `sourceOfTruthPerField`: each field is owned by one side, for example status by ADO and description by Jira
- [ ] `flagForReview`: when both sides changed since the last sync, comment on both items and skip the write

## Maintenance

Ongoing work outside the phases.

- [x] Conformance pinned to the engineering-baseline release v0.2.0, which has the C# rules and `GEN-014` for NuGet
  lock files; Dependabot moves the pin from now on
- [x] HOS waivers removed: `baseline.json` declares `"surface": "api"` (engineering-baseline ADR 7), so the
  static-host header rules no longer apply. The API header rules `HOS-009..012` take their place (Phase 1)

## Later

| Could | Note |
| --- | --- |
| YAML as a second configuration format | Must map to the same model; needs a YAML library (entry in `docs/dependencies.md`) |
| Attachments | Non-goal for now (README) |
| Custom field mapping | Non-goal for now (README) |

## Won't

| Not planned | Why |
| --- | --- |
| Jira Data Center / Server | Jira Cloud only (README, non-goals) |
| Syncing repositories, pipelines or boards | Out of scope: the sync is about work items |
| Secrets in the configuration file | The configuration names the setting; values come from app settings or Key Vault |

## Open decisions

| Decision | Needed for | Source |
| --- | --- | --- |
| Inline processing or a queue: decided, [ADR 0004](adr/0004-acknowledge-and-enqueue-deliveries.md) | Phase 1 | DESIGN.md, section 3 |
| Retry strategy and limits | Phase 1 | DESIGN.md, section 11 |
| Webhook authentication per source: decided, [ADR 0006](adr/0006-webhook-authentication.md) | Phase 1 | DESIGN.md, section 9 |
| JSON Schema check in tests: NJsonSchema, decided (`docs/dependencies.md`) | Phase 1 | Loader and schema can drift |
| Sandbox systems: Jira site, ADO organization and projects | Phase 2 | Needed for tests against real systems |
| Jira webhook per project or global | Phase 2 | DESIGN.md, section 11 |
| Iteration mapping by name or by table | Phase 3 | DESIGN.md, section 11 |
