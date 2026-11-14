# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Roadmap (`docs/roadmap.md`): phases from the walking skeleton (Functions host, REST clients, Table Storage state,
  webhook authentication) to real systems, breadth (creating items, assignee, comments, links, iterations) and
  conflict policies, with releases, open decisions and what is not planned.
- Design document (`docs/DESIGN.md`), MIT licence and an example configuration with its JSON Schema.
- Pure sync engine: echo suppression for the sync's own identity, a hash check over the synced field set,
  status mapping in both directions and last write wins.
- Configuration loader: rejects unknown properties, checks that the status map is one-to-one and reports every error at
  once. The shipped `config/config.example.json` is tested.
- Jira and Azure DevOps webhook parsers and request builders (Jira issue update and transition, ADO JSON Patch with a
  revision test), with plain-text conversion of ADF and HTML that round-trips.
- Project setup after [engineering-baseline](https://github.com/miralem-cebic/engineering-baseline) (profile `csharp`,
  topology `hosted-db`, `baseline.json`): `CLAUDE.md`, Claude Code settings and commands (`/tdd`, `/verify`,
  `/add-adr`), `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md`, ADRs in `docs/adr/`, the dependency register
  `docs/dependencies.md`, `.env.example`, issue and pull request templates and `CODEOWNERS`.
- C# build rules from the baseline templates: `Directory.Build.props` (warnings as errors, `latest-all` analyzers,
  code style in the build, checked arithmetic, no unsafe code, locked and audited restore), central package versions,
  `BannedSymbols.txt`, `global.json` and `.editorconfig`; solution file `JiraAdoSync.slnx` and committed lock files.
- `scripts/verify.sh`, the local gate that CI and the release workflow run.
- CI: ShellCheck, the gate, a vulnerable package report, the Conventional Commit check of PR titles and the baseline
  conformance check in strict mode. Releases from `v*` tags with generated notes. Dependabot for NuGet, the .NET SDK
  and GitHub Actions. `scripts/apply-repo-settings.sh` for the repository settings.
- Sync processor (`SyncProcessor`) and the ports it needs: `IJiraClient`, `IAdoClient` and `ISyncStateStore`. The state
  save uses an ETag, so two concurrent events for one pair cannot both win. Tested against fakes: a delivery sent twice
  writes once, an echo of the sync's own write is not written, an older change does not overwrite a newer one, a failed
  write saves nothing, and a lost save race is reported as a conflict.
- Architecture decisions: changes arrive by webhook, not by polling (`docs/adr/0003-receive-changes-by-webhook.md`);
  deliveries are acknowledged and enqueued, because the vendors' retry rules make inline processing unsafe
  (`docs/adr/0004-acknowledge-and-enqueue-deliveries.md`).
- `SkipReason.NotLinked`: a change to an item without a stored pair is skipped. Creating the item on the other side is
  Phase 3.

- Webhook authentication (`WebhookAuthenticator`, ADR 0006): Jira's HMAC-SHA256 signature over the raw body and the
  ADO basic credentials, both compared in constant time. Malformed headers are rejected without an exception.
- Outbound retry policy (`RetryingHandler`, ADR 0005): retries a 429 for every method, 502, 503 and 504 for reads only,
  honours `Retry-After` up to 30 seconds and makes at most four attempts.
- Jira and Azure DevOps REST clients (`JiraClient`, `AdoClient`) in the new `JiraAdoSync.Adapters` project. They update
  the issue and transition it, and read the work item revision and send it in the JSON Patch test. Their tests use the
  documented response shapes.

### Changed

- engineering-baseline v0.2.0: the `conformance` job is pinned to the release commit, and Dependabot moves the pin.
  `baseline.json` declares `"surface": "api"`, which replaces the four HOS waivers with the API header rules
  `HOS-009..012` (proposed; open until the Functions host sets the headers).
- .NET 10 instead of .NET 8, xUnit v3 on Microsoft.Testing.Platform instead of xUnit v2 (ADR 0002).
- `JiraSettings.SiteUrl` is a `Uri`.
- Configuration DTOs are private and read with a source-generated `JsonSerializerContext`.
