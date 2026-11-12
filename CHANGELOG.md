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

### Changed

- .NET 10 instead of .NET 8, xUnit v3 on Microsoft.Testing.Platform instead of xUnit v2 (ADR 0002).
- `JiraSettings.SiteUrl` is a `Uri`.
- Configuration DTOs are private and read with a source-generated `JsonSerializerContext`.
