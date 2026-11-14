# 5. Outbound retry strategy

Date: 2026-11-14 · Status: accepted

## Context

Jira Cloud and Azure DevOps (ADO) both rate limit requests. Jira answers a limit violation with HTTP 429, a
`Retry-After` header in seconds and a `RateLimit-Reason` header. Its documentation recommends exponential backoff with
jitter, treats `Retry-After` as the minimum wait, and limits writes per issue (20 per 2 seconds, 100 per 30 seconds).
ADO answers a blocked request with HTTP 429. A request that is only delayed gets HTTP 200 with a `Retry-After` header,
and the documentation asks the client to wait before its next request. The status code of a failed `/rev` test is not
known yet (roadmap). The clients do not depend on it.

## Decision

1. A 429 is retried for every method. The vendors describe a limit violation as a rejection, not as a partly processed
   request. If a write was applied anyway, the `/rev` test (ADO) and the idempotent PUT (Jira) make the retry safe. A
   repeated transition fails, and the event is decided again from fresh state.
2. The wait is the `Retry-After` value when the header is present, and it is the minimum wait. Otherwise the wait is
   exponential: 1 second, then 2, then 4, and so on, scaled by a random factor between 0.7 and 1.3.
3. No single wait is longer than 30 seconds. A `Retry-After` longer than that is not waited for. The 429 is returned,
   the write fails, and the queue retries the event later (ADR 0004).
4. A request is attempted at most four times. After the last attempt the response is returned, and the client throws.
5. 502, 503 and 504 are retried with the same waits, for GET requests only. A write that fails with one of these codes
   may or may not have been applied, so the handler does not retry it. The queue decides again from fresh state.
6. A successful response with `Retry-After` (ADO's delayed requests) makes the handler wait before it returns the
   response, if the wait is at most 30 seconds. ADO asks the client to wait in that case.

## Consequences

- One policy in one `DelegatingHandler` (`RetryingHandler`) in the adapters project, shared by both clients.
- A request takes at most three waits of 30 seconds, which is 90 seconds, before its error reaches the queue.
- The tests use the documented response shapes. Phase 2 confirms them with recorded responses.
