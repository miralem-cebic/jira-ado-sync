# Dependencies

Every package needs an entry here with its reason. Versions are set once in `Directory.Packages.props` and locked in
the `packages.lock.json` files; Dependabot proposes updates weekly.

## Runtime

Besides the .NET 10 base class library, which supplies JSON, hashing, HTML encoding and regular expressions, the host
uses these packages. The core and the Jira and ADO adapters use none of them. The Azure packages stay in
`JiraAdoSync.Azure` and `JiraAdoSync.Functions`.

| Package | Purpose | Native alternative considered | Licence |
|---|---|---|---|
| `Azure.Storage.Blobs` | Raw deliveries in a blob container (ADR 0004) | None: the REST API directly would duplicate the SDK's signing and retry | MIT |
| `Azure.Storage.Queues` | The delivery queue between receiver and processor (ADR 0004) | None, as above | MIT |
| `Azure.Data.Tables` | The `SyncItems` table with the ETag-checked saves (DESIGN.md, section 4) | None, as above | MIT |
| `Azure.Identity` | The managed identity of the Function for Azure DevOps (`DefaultAzureCredential`) | A PAT only: rejected, because it is a long-lived secret (DESIGN.md, section 9) | MIT |
| `Azure.Core` (transitive) | Token and client infrastructure of the Azure SDK | — | MIT |
| `Microsoft.Azure.Functions.Worker` 2.52.0, with `Worker.Core` and `Worker.Grpc` (transitive) | The isolated worker host of the Functions runtime | The in-process model: not available for .NET 10 isolated | MIT |
| `Microsoft.Azure.Functions.Worker.Extensions.Http` 3.3.0 | HTTP triggers of the two webhook endpoints | None | MIT |
| `Microsoft.Azure.Functions.Worker.Extensions.Storage.Queues` 5.5.5 | The queue trigger of the processor | None | MIT |
| `Microsoft.Azure.Functions.Worker.Sdk` 2.1.0 | Build-time generation of the function metadata. Not shipped at run time | None | MIT |
| `Microsoft.Extensions.Hosting` 10.0.15 | The generic host that the worker runs in, with dependency injection and hosted services | None | MIT |
| `Microsoft.Extensions.Http` 10.0.15 | Typed `HttpClient`s with the handler chain of the retry policy and the authorization | Creating `HttpClient` by hand: rejected, because it leaks sockets and does not compose handlers | MIT |
| `Grpc.Net.Client`, `Grpc.Core.Api`, `Google.Protobuf` (transitive, through `Worker.Grpc`) | The channel to the Functions host | None: required by the worker | Apache-2.0, Apache-2.0, BSD-3-Clause |

## Build and test

| Package | Purpose | Native alternative considered | Licence |
|---|---|---|---|
| `Microsoft.CodeAnalysis.BannedApiAnalyzers` | Enforces `BannedSymbols.txt` (no wall clock, no blocking waits) | None in the SDK | MIT |
| `xunit.v3.mtp-v2` | Test framework on Microsoft.Testing.Platform | MSTest (Microsoft): xUnit v3 is what the other baseline projects use and measure | Apache 2.0 |
| `NJsonSchema` 11.6.2, with its transitive `Newtonsoft.Json`, `Namotion.Reflection` and `NJsonSchema.Annotations` | Validates `config.example.json` and `config.schema.json` in `ConfigSchemaTests`, so the schema for editors and the loader cannot drift. Test only, not in the shipped package. NJsonSchema reads every schema as draft 4, so the schema uses only keywords that draft 4 and 2020-12 share | The base class library has no JSON Schema validator. JsonSchema.Net was considered: it supports draft 2020-12, but its binary package is offered under a maintenance-fee agreement for revenue-generating users with at least US$10,000 gross revenue (the source is MIT). A hand-written validator would test our own code | MIT (all four packages) |
