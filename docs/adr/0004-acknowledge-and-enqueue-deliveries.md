# 4. Acknowledge and enqueue webhook deliveries

Date: 2026-11-14 · Status: accepted

## Context

The receiver can process a delivery before it answers (inline), or store it and answer at once (acknowledge and
enqueue). The roadmap leaves this open: it depends on how long each source waits for a response and how it retries.
The vendor documentation answers it:

- **ADO** retries only 408, 502, 503 and 504: up to eight times, the backoff capped at 60 seconds, about three minutes
  in total. After that the notification is dropped. Any other failed response, such as 500 or 429, puts the
  subscription on probation, and during probation new events are lost. The response timeout is not stated.
- **Jira** retries up to five times, 5 to 15 minutes apart, on 408, 409, 425, 429, any 5xx, a failed connection or a
  timeout. Deliveries can be duplicated. The response timeout is not stated.

Inline processing makes each response wait for Jira and ADO calls. Those calls can be slow or rate limited (429). A rate
limit that lasts longer than ADO's retries becomes an enduring failure, which causes probation. Azure Queue Storage
messages are limited to 64 KiB, and an ADO payload can be up to 2 MB, so the message cannot carry the payload.

## Decision

1. The receiver checks the source's authentication, checks that the body is JSON and stores the raw body in Blob
   Storage. The blob name is the source and the SHA-256 of the body, so a redelivery writes the same blob. It then
   enqueues a message with the source and the blob name, and answers `202 Accepted`.
2. A queue-triggered processor reads the blob, parses it with the core parsers and runs the sync.
3. The receiver answers `503` when it cannot store or enqueue, so both vendors retry. It never answers `429` or `410`
   to a delivery, and it never answers `500` for its own failure.
4. A body that does not parse as a supported event is dropped by the processor and not retried. A failure that can
   succeed later throws, and the queue retries it. After the queue's retry limit the message goes to the poison queue.

## Consequences

- The response to a delivery does not depend on Jira or ADO. Rate limits and slow calls only delay processing, and the
  queue keeps the event until it is done.
- Raw deliveries contain personal data (names, descriptions). They are kept for a fixed period (proposed: seven days,
  with a lifecycle rule on the container), never logged, and read only by the processor.
- The poison queue must be watched. An event there is a change that was not synced (Phase 2).
- A write to the target can succeed before the state is saved. If the save loses a race, the event is processed
  again and writes the same values again. That is harmless, and it is the price of not using a distributed transaction.
- The Phase 1 host needs an HTTP trigger, a queue trigger and Blob Storage. The packages are recorded in
  `docs/dependencies.md` when they are added.
