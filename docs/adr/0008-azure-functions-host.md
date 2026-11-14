# 8. Azure Functions host

Date: 2026-11-14 · Status: accepted

## Context

The core, the adapters and the Azure storage adapters exist. The host connects them to the Functions runtime. ADR 0002
left the `CA2007` decision for the host project open. ADR 0004 and ADR 0007 fix the flow and the status codes the host
must keep.

## Decision

1. The host is an isolated worker on .NET 10 (Functions v4). It has three triggers: an HTTP `POST` on
   `/api/webhooks/jira` and on `/api/webhooks/ado`, both anonymous because the receiver authenticates (ADR 0006), and a
   queue trigger on `sync-deliveries` (ADR 0004).
2. Startup validates the configuration and reads the secrets it names from app settings or environment variables. A
   missing secret or an invalid configuration stops the host with a message that names the setting, never its value.
3. The storage connection is the `AzureWebJobsStorage` setting, which the runtime needs for its own triggers. Access by
   managed identity is not configured yet. It belongs to the deployment definition in Phase 2.
4. Azure DevOps is called with the managed identity of the Function, through a bearer token for the Azure DevOps
   resource. A personal access token is for local testing only, and it is sent as basic credentials with an empty user
   name.
5. The delivery queue client comes from `StorageClients.CreateDeliveryQueue`, which sets Base64 message encoding. Base64
   is what the Functions queue trigger expects by default. The host and the storage tests use this one factory.
6. Every response of the webhook endpoints carries the baseline security headers (`ResponseHeaders`). The status comes
   from the receiver (ADR 0007). An exception around the receiver becomes a `503`, never an unhandled `500`.
7. The host writes one line when a delivery is answered and one when it is processed. Each carries the source, the item
   key or work item id, the blob name and the decision. Bodies, headers and secrets are never logged.
8. `CA2007` stays on for the host, and every `await` uses `ConfigureAwait(false)`. The worker has no synchronization
   context, so this changes nothing at runtime, but the code stays the same as in the core. `CA1515` is off for the
   Functions project only, because the worker finds its public function classes by reflection.

## Consequences

- The host starts only with every secret set. A deployment must set them before the first request.
- A local run needs Azurite started with `--skipApiVersionCheck`. The current Azure SDK sends an API version that
  Azurite 3 does not accept. The storage tests pin the emulator's versions. See `docs/running-locally.md`.
- The queue trigger retries a failed message and then moves it to the poison queue (after five attempts, the runtime
  default). The poison queue must be watched (Phase 2).
- A table query by work item id has no index. That is acceptable at the expected volume. It is revisited with the first
  real volume.
