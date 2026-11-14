# 7. Response codes of the webhook receiver

Date: 2026-11-14 · Status: accepted

## Context

ADR 0003 and ADR 0004 set how the vendors treat a response. ADO retries only 408, 502, 503 and 504, for about three
minutes. Any other failed response puts the subscription on probation, where new events are lost. 410 disables it at
once. Jira retries 408, 409, 425, 429 and every 5xx up to five times, 5 to 15 minutes apart. ADR 0004 left the codes
open for three cases: a body that is not JSON, an event type the sync does not handle, and a failed write of the body
or the message.

## Decision

| Result | Status | When |
| --- | --- | --- |
| `Accepted` | `202 Accepted` | Authenticated, JSON, stored and enqueued. This includes event types the sync does not handle. The processor drops those, and a vendor retry would not change that. |
| `Unauthorized` | `401 Unauthorized` | The credential is missing or wrong (ADR 0006). For ADO this is an enduring failure, which is the signal a wrong credential should give. |
| `NotJson` | `400 Bad Request` | The request is authenticated, but the body is not JSON. For ADO this is an enduring failure. Only a misconfigured sender produces it. |
| `Unavailable` | `503 Service Unavailable` | The body or the message could not be written. Both vendors retry it. |

The receiver never answers `500` for its own failure, and it never answers `429` or `410`. The mapping is
`ReceiveResultExtensions.ToHttpStatusCode`, and a test keeps those three codes out of it.

## Consequences

- Event types the sync ignores are stored too. They are kept for the retention period of the raw bodies (proposed:
  seven days).
- A sender that does not send JSON puts an ADO subscription on probation. That is intended, because it needs fixing.
- The host must map the result with `ToHttpStatusCode`. It must also catch exceptions around the receiver and answer
  `503`. An unhandled exception in an Azure Function becomes a `500`, and ADO treats that as enduring.
