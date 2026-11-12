---
description: Implement a change test-first
argument-hint: <what to build or fix>
---

Implement: $ARGUMENTS

1. Restate the behaviour as one or more test cases. Ask if the intended behaviour is ambiguous.
2. Write the failing test(s) first. Run `dotnet test --solution JiraAdoSync.slnx` and show that they fail for the
   right reason.
3. Write the smallest implementation that makes them pass. Keep `JiraAdoSync.Core` free of I/O.
4. Refactor with the tests green.
5. Run `scripts/verify.sh` and read the output. Report failures honestly; do not claim done until it is green.
