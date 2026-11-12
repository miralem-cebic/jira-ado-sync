# jira-ado-sync

A configurable, bidirectional sync between **Jira Cloud** and **Azure DevOps** work items, built as an Azure Function.

Atlassian's built-in Azure DevOps connector only syncs in one direction. This project aims to keep both systems in step, so that teams working in either tool see the same state.

> **Status:** early design. See [docs/DESIGN.md](docs/DESIGN.md). Nothing is production-ready yet.

## Goals

- Bidirectional sync of work items between Jira Cloud and Azure DevOps.
- Everything environment-specific is configuration, not code: sites, organizations, project mappings, status mappings, field selection, conflict policy.
- Safe to run against real data: loop prevention, idempotent processing, and a clear conflict policy.
- Deployable as an Azure Function, with infrastructure documented as an example.

## Non-goals (for now)

- Jira Data Center / Server.
- Attachments and custom field mapping (planned later, see design doc).
- Syncing repositories, pipelines, or boards.

## How it works (overview)

```
Jira Cloud --webhook--> Azure Function --REST--> Azure DevOps
Jira Cloud <--REST----- Azure Function <--service hook-- Azure DevOps
                             |
                       Azure Table Storage
                  (ID mapping + last-sync state)
```

See [docs/DESIGN.md](docs/DESIGN.md) for the details.

## Configuration

Non-secret settings live in a JSON file. See [config/config.example.json](config/config.example.json) for a fully commented example.

Secrets (Jira API token, ADO PAT if used) are never stored in the config file. They come from application settings or Azure Key Vault.

## License

MIT. See [LICENSE](LICENSE).
