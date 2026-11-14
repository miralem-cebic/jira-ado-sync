# 3. Receive changes by webhook, not by polling

Date: 2026-11-14 · Status: accepted

## Context

A change in Jira or Azure DevOps (ADO) has to reach the other side within seconds to minutes. Polling both APIs would
spend rate limit quota on reads that mostly find nothing, and it would add a fixed delay. Both systems can push changes
to an HTTPS endpoint: Jira Cloud with webhooks, ADO with service hooks (the Web Hooks service). [DESIGN.md](../DESIGN.md),
section 3, already assumes this.

## Decision

Jira and ADO deliver each change as a webhook to the Function. The sync does not poll either system in v1.

## Consequences

- Deliveries can repeat, arrive late or arrive out of order. Processing is idempotent (DESIGN.md, section 6) and
  ignores older changes (last write wins), as ADR 0004 and the core already do.
- Jira webhooks registered through the REST API expire 30 days after they were created or last refreshed. The refresh
  call (`PUT /rest/api/3/webhook`) has to run on a schedule, or the webhook stops delivering. The Jira documentation
  does not say whether webhooks registered in the admin UI expire; this is checked in Phase 2.
- ADO puts a subscription on probation after failures it cannot retry, and disables it after seven failed probation
  retries. A subscription with no successful delivery for six months is also disabled. Delivery health must be
  watched on the Service Hooks page (Phase 2).
- Webhook authentication is per source (DESIGN.md, section 9). Jira signs deliveries of admin webhooks that have a
  secret with an HMAC in the `X-Hub-Signature` header. ADO authenticates with basic authentication, which requires
  HTTPS. The exact checks are decided in their own record.
