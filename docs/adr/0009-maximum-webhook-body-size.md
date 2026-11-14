# 9. Maximum size of a webhook body

Date: 2026-11-14 · Status: accepted. It adds a result to the response codes of ADR 0007.

## Context

The receiver read the whole request body into memory before it authenticated the request. This was found in the review
of PR #7. Anyone who could reach an endpoint could make the host allocate memory without a limit. Azure DevOps documents
a 2 MB payload limit for service hooks. The sources used for this project do not document a maximum size for Jira
webhook bodies, so the Jira limit is an assumption until real deliveries are recorded (roadmap, Phase 2).

## Decision

1. A request body is read up to 2 MiB (2,097,152 bytes). The constant is `WebhookLimits.MaxBodyBytes`.
2. A `Content-Length` above the limit is answered `413 Payload Too Large`, and the body is not read.
3. A body that grows past the limit while it is read is answered `413`. The read stops after the first chunk beyond the
   limit.
4. Both checks run before authentication, before parsing and before anything is stored.
5. The response table of ADR 0007 gains the result `TooLarge` with status `413`.

## Consequences

- An Azure DevOps delivery cannot exceed the documented 2 MB. A `413` for an ADO request therefore means a sender that
  is not ADO, or a misconfigured hook. For ADO, `413` is an enduring failure, like `400` (ADR 0003), so the operators
  see the problem.
- Jira deliveries above 2 MiB are rejected. Phase 2 records real deliveries and confirms their size. A different limit
  needs a new ADR.
- The limit is a constant in the code and not a setting. It describes the vendors, not one tenant.
