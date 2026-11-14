# 10. Retry window and poison queue of the delivery queue

Date: 2026-11-14 · Status: accepted

## Context

`host.json` set no queue settings, so the Functions queue trigger used its defaults. Those give five attempts with a
short delay in between, so the retry window was a matter of seconds. A Jira or ADO outage that lasted longer moved the
affected changes to the poison queue. In the local run, messages that could not be decoded reached the poison queue
within a few seconds. Azure Functions documents `visibilityTimeout` as the time between retries of a failed message, and
`maxDequeueCount` as the number of attempts before the message moves to the poison queue (`<queue>-poison`).

## Decision

1. `host.json` sets `visibilityTimeout` to one minute and `maxDequeueCount` to 10. A message is retried for about ten
   minutes.
2. A message that still fails is in `sync-deliveries-poison`. Recovering it is a replay by hand, described in
   `docs/runbooks/poison-queue.md`. `scripts/replay-poison.py` moves the messages.
3. A replay needs the raw body. It is kept while the lifecycle rule of ADR 0004 keeps it. Phase 2 deploys that rule.

## Consequences

- Receiving and processing have separate windows. ADO retries a `503` for about three minutes (ADR 0003), and Jira for
  up to 75 minutes. Processing retries the queue for about ten minutes, which covers a short outage of Jira or ADO.
- An outage that lasts longer ends in the poison queue. The replay recovers those changes while their bodies exist.
- The alert on the poison queue is a Phase 2 item. Until it exists, the poison queue is checked by hand.
