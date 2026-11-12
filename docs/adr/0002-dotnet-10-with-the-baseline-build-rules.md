# 2. .NET 10 with the engineering-baseline build rules

Date: 2026-11-12 · Status: accepted

## Context

The project started on .NET 8 with the SDK defaults: warnings did not fail the build, analyzers ran at their default
level and package versions lived in each project file. The sync writes to two production systems, so a defect costs
more than in most tools. The other C# projects on engineering-baseline (INI-Korrektur, itsmesm) run .NET 10 with the
baseline's C# rules (`CSH-001..018`) and pass them.

## Decision

- Target .NET 10 (LTS), pinned in `global.json`, with the files from `templates/csharp/` of engineering-baseline:
  `Directory.Build.props`, `Directory.Packages.props`, `BannedSymbols.txt`, `.editorconfig`.
- Every warning is an error, analyzers run at `latest-all`, code style is enforced in the build, arithmetic is checked,
  unsafe code is off, restore is locked and audited.
- Tests use xUnit v3 on Microsoft.Testing.Platform, as in itsmesm.
- The local gate is `scripts/verify.sh`; CI and the release workflow run the same script.
- Configuration DTOs are deserialized with a source-generated `JsonSerializerContext` instead of reflection.

## Consequences

- Public API carries XML documentation, argument checks and explicit string comparisons; the build refuses code
  without them.
- A suppression needs a justification next to it (`.editorconfig` or `[SuppressMessage]`), never a blanket `NoWarn`.
  The first one is `CA1034` on `SyncDecision`, whose cases are nested on purpose (closed union).
- `CA2007` (`ConfigureAwait`) stays on as in the template. The Core library has no async code yet; when the Azure
  Functions host is added, its project decides with a recorded reason, as itsmesm did for its ASP.NET Core host.
- Contributors need the .NET 10 SDK from `global.json`.
