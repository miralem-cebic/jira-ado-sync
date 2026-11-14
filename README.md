# jira-ado-sync

[![CI](https://github.com/miralem-cebic/jira-ado-sync/actions/workflows/ci.yml/badge.svg)](https://github.com/miralem-cebic/jira-ado-sync/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/github/license/miralem-cebic/jira-ado-sync)](LICENSE)

A configurable, bidirectional sync between **Jira Cloud** and **Azure DevOps** work items, built as an Azure Function.

Atlassian's built-in Azure DevOps connector only syncs in one direction. This project aims to keep both systems in step, so that teams working in either tool see the same state.

> **Status:** early design. The pure core (sync engine, configuration loader, webhook parsers, request builders and the sync processor with its ports) exists and is tested; the Azure Functions host, the REST clients and the state store do not exist yet. See [docs/DESIGN.md](docs/DESIGN.md) and the [roadmap](docs/roadmap.md). Nothing is production-ready yet.

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

Non-secret settings live in a JSON file, validated against [config/config.schema.json](config/config.schema.json). See [config/config.example.json](config/config.example.json) for an example.

Secrets (Jira API token, ADO PAT if used) are never stored in the config file. The config names the setting that holds them; the value comes from application settings or Azure Key Vault. [.env.example](.env.example) lists them.

## Development

Prerequisite: the .NET SDK named in [global.json](global.json) (.NET 10).

```bash
scripts/verify.sh
```

That is the gate CI runs: restore from the lock files, build with every warning as an error, tests, and `dotnet format --verify-no-changes`. The working agreement (test first, invariants, definition of done) is in [CLAUDE.md](CLAUDE.md).

| Path | Content |
|---|---|
| `src/JiraAdoSync.Core/` | Sync engine, configuration, Jira and Azure DevOps payloads. No I/O. |
| `tests/JiraAdoSync.Core.Tests/` | Unit tests (xUnit v3) |
| `config/` | JSON Schema and example configuration |
| `docs/` | [Design](docs/DESIGN.md), [roadmap](docs/roadmap.md), [decisions](docs/adr/README.md), [dependencies](docs/dependencies.md) |

## Engineering baseline

The quality rules follow [engineering-baseline](https://github.com/miralem-cebic/engineering-baseline). [baseline.json](baseline.json) sets the profile: ecosystem `csharp`, topology `hosted-db`, surface `api` (a webhook API without browser content, so the API header rules apply instead of the static-site ones). CI runs the conformance check of release v0.2.0 in strict mode; locally:

```bash
npx github:miralem-cebic/engineering-baseline check
```

The repository settings (squash merges, branch protection, secret scanning) are applied with [scripts/apply-repo-settings.sh](scripts/apply-repo-settings.sh):

```bash
scripts/apply-repo-settings.sh miralem-cebic/jira-ado-sync "verify,pr-title,conformance / check"
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) and the [Code of Conduct](CODE_OF_CONDUCT.md). Report security issues privately, see [SECURITY.md](SECURITY.md). Changes are listed in [CHANGELOG.md](CHANGELOG.md).

## License

MIT. See [LICENSE](LICENSE).
