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
| `NJsonSchema` 11.6.2, with its transitive `Newtonsoft.Json`, `Namotion.Reflection` and `NJsonSchema.Annotations` | Validates `config.example.json` and `config.schema.json` in `ConfigSchemaTests`, so the schema for editors and the loader cannot drift. Test only, not in the shipped package. NJsonSchema reads every schema as draft 4, so the schema uses only keywords that draft 4 and 2020-12 share | The base class library has no JSON Schema validator. JsonSchema.Net was considered: it supports draft 2020-12, but its binary package is offered under a maintenance-fee agreement for revenue-generating users with at least US$10,000 gross revenue (the source is MIT). A hand-written validator would test our own code | MIT (all four packages) |
