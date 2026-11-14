# 11. Tolerate unmapped Jira states with ignoreJiraStates

Date: 2026-11-14 · Status: accepted.

## Context

Jira projects often carry more workflow states than the destination Azure DevOps process has. The sandbox project
M51 has nine Jira statuses (`Backlog`, `On Hold`, `To Do`, `In Progress`, `Test`, `Waiting for external`,
`Waiting for Internal`, `Closed`, `Rejected`), while Neo on Azure DevOps has five
(`New`, `Active`, `Resolved`, `Closed`, `Removed`). The configuration enforces a one-to-one status map so that the
reverse direction stays unambiguous, which leaves four Jira statuses without a target.

The engine answered a Jira event in an unmapped state with `Skip(UnmappedStatus)` (`SyncEngine.cs:39`), which also
blocked title and description from reaching ADO. In practice an issue in `On Hold` or `Waiting for external` can
still have its title or description edited, and that edit should propagate.

## Decision

1. Each project mapping gains an optional array `ignoreJiraStates`. Entries are case-sensitive Jira status names.
2. The loader rejects a configuration where a Jira status appears in both `statusMap` and `ignoreJiraStates`, where
   an entry is empty or whitespace, or where an entry is listed more than once (the schema also forbids duplicates
   via `uniqueItems`).
3. When a Jira event arrives with a status in `ignoreJiraStates`, the sync engine treats status as not supplied for
   that event: the canonical item carries `JiraStatus = null`, the hash is computed without status, and the Apply
   decision writes no `System.State` to the work item on the ADO side (`AdoPatchBuilder.cs:42` already omits the
   operation when `JiraStatus` is null).
4. `Skip(UnmappedStatus)` still applies to Jira statuses that are neither mapped nor ignored, and to ADO states
   whose reverse lookup finds no Jira status.
5. Echo suppression and the hash check are unchanged. A Jira event from the sync's own identity in an ignored state
   still ends as `Skip(EchoSuppressed)`, tested by `SyncEngineTests.EchoFromAnIgnoredStateIsStillSuppressed`.

## Consequences

- Title, description and assignee flow in the Jira → ADO direction while an issue sits in an ignored state, and the
  ADO work item keeps whatever state it had last.
- The reverse direction (ADO → Jira) is not changed. A new mapping lookup still finds the one Jira status that pairs
  with the incoming ADO state, and `JiraClient` is free to transition Jira. Writing from ADO while the Jira issue is
  in an ignored state can therefore move Jira out of that state, which is a known limitation: it needs stored
  knowledge of the last Jira status and is left for a later ADR.
- The one-to-one invariant of `statusMap` stays. A Jira status is either in the map (synced in both directions) or
  on the ignore list (tolerated in the Jira → ADO direction).
- The feature is additive. Existing configurations without `ignoreJiraStates` keep their behaviour: an event in an
  unmapped state is still skipped.
