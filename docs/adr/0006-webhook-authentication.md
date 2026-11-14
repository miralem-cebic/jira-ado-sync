# 6. Webhook authentication per source

Date: 2026-11-14 · Status: accepted

## Context

A request without a valid credential is rejected (roadmap, Phase 1; [DESIGN.md](../DESIGN.md), section 9). The two
sources authenticate differently:

- **Jira** signs each delivery of a webhook that is registered with a secret. The signature is an HMAC in the
  `X-Hub-Signature` header, in the form `method=signature`. The examples use SHA-256. Webhooks of OAuth 2.0 apps use
  bearer authentication, and its header is not documented.
- **ADO** service hooks authenticate with basic authentication. The documentation says it must use HTTPS. It describes
  no request signature.

## Decision

1. **Jira:** the receiver computes HMAC-SHA256 over the raw request body with the webhook's secret. It compares the
   result with the hex value after `sha256=` in `X-Hub-Signature`, in constant time. A missing header, another method
   prefix, a value that is not 64 hex digits, or a mismatch is rejected.
2. **ADO:** the receiver requires `Authorization: Basic …` and compares the user name and the password with the
   configured values. Both comparisons run in constant time, and both always run.
3. Both checks run before the body is parsed or stored. A rejected request gets `401 Unauthorized`.
4. The secrets are read from the settings that the configuration names (DESIGN.md, section 9). The configuration never
   holds a value.
5. The Jira check uses the raw bytes of the body, never a re-serialised body.

## Consequences

- Phase 2 confirms the header format with a recorded Jira delivery. The documentation gives the pattern and the SHA-256
  example only.
- A `401` is an enduring failure for ADO, which puts the subscription on probation (ADR 0003). A wrong ADO credential
  is noticed quickly, but events are lost until it is fixed.
- Bearer authentication is not implemented, because the sync uses webhooks with a secret.
- Jira webhooks must be registered with a secret. Phase 2 checks that for the chosen registration method.
