# Dependencies

Every package needs an entry here with its reason. Versions are set once in `Directory.Packages.props` and locked in
the `packages.lock.json` files; Dependabot proposes updates weekly.

## Runtime

**None** besides the .NET 10 base class library. JSON (`System.Text.Json`), hashing
(`System.Security.Cryptography`), HTML encoding (`System.Net.WebUtility`) and regular expressions come from the
platform.

Planned with the Azure Functions host, each to be added here when it is introduced: the Azure Functions isolated
worker, the Azure Table Storage client and `Azure.Identity` for the managed identity. All three are Microsoft
packages; they stay out of `JiraAdoSync.Core`.

## Build and test

| Package | Purpose | Native alternative considered | Licence |
|---|---|---|---|
| `Microsoft.CodeAnalysis.BannedApiAnalyzers` | Enforces `BannedSymbols.txt` (no wall clock, no blocking waits) | None in the SDK | MIT |
| `xunit.v3.mtp-v2` | Test framework on Microsoft.Testing.Platform | MSTest (Microsoft): xUnit v3 is what the other baseline projects use and measure | Apache 2.0 |
