# CLAUDE.md

jira-ado-sync keeps Jira Cloud issues and Azure DevOps work items in step, in both directions, as an Azure Function.
The design is in [docs/DESIGN.md](docs/DESIGN.md); decisions are in [docs/adr/](docs/adr/README.md).

## Commands

```bash
scripts/verify.sh                                   # the gate: everything below, as CI runs it
dotnet restore JiraAdoSync.slnx --locked-mode       # versions come from the lock files
dotnet build JiraAdoSync.slnx -c Release            # zero warnings, they are errors
dotnet test --solution JiraAdoSync.slnx -c Release  # xUnit v3 on Microsoft.Testing.Platform
dotnet format JiraAdoSync.slnx --verify-no-changes  # formatting and code style
npx github:miralem-cebic/engineering-baseline check # conformance with the baseline (Node 22+)
```

After changing a package version in `Directory.Packages.props`, run `dotnet restore JiraAdoSync.slnx` once without
`--locked-mode` and commit the updated `packages.lock.json` files.

## Hard invariants

- **Test first.** Write the failing test, watch it fail for the right reason, then implement. A bug fix starts with a
  test that reproduces the bug.
- **Loop prevention is never weakened.** Echo suppression (the sync's own identity) and the hash check
  (`LastSyncedHash`) are both required ([DESIGN.md](docs/DESIGN.md), section 6). A change to either needs a test for
  the echo case.
- **Processing is idempotent.** Webhooks arrive more than once and out of order. The same event twice never writes
  twice; an older event never overwrites a newer change.
- **`JiraAdoSync.Core` is pure.** No HTTP, no storage, no clock, no environment. Time comes in with the event; where a
  clock is needed, inject `TimeProvider` (`DateTime.UtcNow` and friends are banned in `BannedSymbols.txt`).
- **Webhook payloads are untrusted input.** Parsers never throw on an unexpected shape; they return null for what they
  do not understand. Only a body that is not JSON at all throws.
- **Nothing environment-specific in code.** Sites, organizations, projects, status maps and synced fields are
  configuration (`config/config.schema.json`). The loader rejects unknown properties and reports every error at once.
- **No secrets** in configuration, code, tests, examples or logs. The configuration names the setting that holds a
  secret, never the secret itself. Test data is invented, never copied from a real Jira site or ADO organization.
- **Never weaken the build.** Fix the code, or suppress one diagnostic in `.editorconfig` or with
  `[SuppressMessage(Justification = "...")]`. Never a blanket `NoWarn` (baseline rule CSH-016).
- **A new dependency** needs an entry in [docs/dependencies.md](docs/dependencies.md) with its reason. The .NET
  platform comes first.

## Definition of done

`scripts/verify.sh` is green and its output was read, not assumed. Behaviour changes have a test that failed first.
`CHANGELOG.md` has an entry under `[Unreleased]`. README, `docs/DESIGN.md` and the configuration schema and example
match the change. Commits and PR titles are Conventional Commits; PRs are squash-merged.

CI runs more than `scripts/verify.sh`: ShellCheck, the vulnerable package report, `pr-title` and `conformance`
(engineering-baseline in strict mode).

## Layout

| Path | Purpose |
|---|---|
| `src/JiraAdoSync.Core/Sync/` | `SyncEngine`: the pure decision (echo, unmapped status, hash, last write wins) |
| `src/JiraAdoSync.Core/Jira/`, `Ado/` | Webhook parsers and request builders per side, no HTTP |
| `src/JiraAdoSync.Core/Config/` | Configuration loader and the validated model |
| `src/JiraAdoSync.Core/Model/` | Side-neutral types: `CanonicalItem`, `SyncEvent`, `SyncState` |
| `tests/JiraAdoSync.Core.Tests/` | Unit tests; the shipped `config.example.json` is tested too |
| `config/` | JSON Schema and example configuration |
| `docs/DESIGN.md`, `docs/adr/` | Architecture and decisions (`/add-adr`) |

## Engineering baseline

The project follows [engineering-baseline](https://github.com/miralem-cebic/engineering-baseline): profile `csharp`,
topology `hosted-db` (`baseline.json`). A waiver there needs a reason and an expiry date. A C# finding that applies
beyond this repository (build settings, analyzers, `.editorconfig`, `BannedSymbols.txt`, test projects) also goes
upstream as a pull request to engineering-baseline.
