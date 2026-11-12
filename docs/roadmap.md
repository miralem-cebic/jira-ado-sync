# Roadmap

jira-ado-sync keeps Jira Cloud issues and Azure DevOps work items in step, in both directions, as an Azure Function.
The design is in [DESIGN.md](DESIGN.md); this file plans the way there. Status: 12 November 2026.

Legend: `[ ]` open · `[~]` in progress · `[x]` done · `[!]` blocked

## Guardrails

- **Walking skeleton before breadth.** Title, description and status run in both directions, end to end against real
  systems, before a new field or feature is added.
- **The core stays pure.** HTTP, storage, clock and configuration sources live in the host and adapter projects behind
  interfaces that `JiraAdoSync.Core` defines. Third-party and Azure SDK types do not reach the core.
- **Nothing is ignored silently.** A setting the sync cannot honour yet is not in the schema or the loader. Comments,
  links and iterations were removed from the configuration for that reason and come back with their implementation.
- **Real payloads beat documentation.** Where the vendor documentation is ambiguous, a recorded delivery becomes a test
  fixture and decides.
- Every phase ends with something that runs and can be shown.

## Releases

| Release | Content |
| --- | --- |
| **0.1.0** | Phases 1 and 2: title, description and status in both directions, deployed to Azure, proven in a sandbox |
| 0.x | Phase 3: creating items, assignee, comments, links, iterations |
| 1.0 | Phase 4 (conflict policies), plus everything above running against real projects without manual repair |

## Phase 0: foundation (done)

- [x] Design document, MIT licence, example configuration
- [x] Pure sync engine: echo suppression for the sync's own identity, hash check over the synced field set, status
  mapping in both directions, last write wins
- [x] JSON configuration with JSON Schema (Draft 2020-12) and loader: unknown properties are rejected, all errors are
  reported at once, the shipped example is tested; secrets are referenced by setting name only
- [x] Webhook parsers for `jira:issue_created`/`updated` and `workitem.created`/`updated`, request builders (Jira issue
  update, transition lookup and transition, ADO JSON Patch with a `test` on `/rev`), ADF and HTML to plain text
- [x] Loop freedom proven in the core: a Jira change is written to ADO, and its echo from ADO is `NoChange`, also with
  a different HTML line break form
- [x] engineering-baseline adopted: C# build rules on .NET 10, pipeline, project files
  ([#1](https://github.com/miralem-cebic/jira-ado-sync/pull/1)); CI and Dependabot green
- [x] Repository settings applied with `scripts/apply-repo-settings.sh`: `main` is protected, pull requests are
  squash-merged and their branches deleted on merge (seen on
  [#2](https://github.com/miralem-cebic/jira-ado-sync/pull/2))
- [x] Private vulnerability reporting is enabled; `SECURITY.md` points to it

Moved out of this phase, because they are not foundation work: the schema check (Phase 1) and the conformance pin
(Maintenance, waiting for engineering-baseline).

## Phase 1: walking skeleton

Goal: a change of title, description or status travels Jira → ADO and ADO → Jira through the Function, and its echo is
not written back.

- [ ] Decision (ADR): process a webhook inline, or acknowledge and enqueue it (DESIGN.md, section 3). Depends on how
  long each source waits for a response and how it retries
- [ ] Interfaces in the core for the Jira client, the ADO client and the state store; `TimeProvider` where a clock is
  needed
- [ ] Check `config.example.json` against `config.schema.json` in a test, so the schema (for editors) and the loader
  (authoritative) cannot drift once this phase adds settings (webhook secrets, the sync's own Jira account and ADO
  identity). .NET has no JSON Schema validator: needs a decision on a package (`docs/dependencies.md`) or another way
- [ ] Jira REST client (API v3): read issue, update summary and description, list transitions and transition; API
  token of a dedicated service account
- [ ] ADO REST client (7.1): read work item, JSON Patch with the `/rev` test; managed identity, PAT for local testing
  only
- [ ] Retry strategy (ADR, DESIGN.md section 11): back off on rate limits (429, honour `Retry-After`) on both sides.
  When the `/rev` test fails, read the item again and let the engine decide again instead of overwriting. Confirm the
  status code of a failed test with a real response
- [ ] Contract tests for both clients against recorded responses (DESIGN.md, section 10)
- [ ] State store on Azure Table Storage: table `SyncItems` as in DESIGN.md section 4. Writes use the entity ETag, so
  two concurrent events for one pair cannot both win
- [ ] Azure Functions host (isolated worker, .NET 10) with HTTP triggers for the Jira webhook and the ADO service hook
- [ ] Webhook authentication: requests without a valid secret are rejected (shared secret or HMAC, per source;
  DESIGN.md section 9)
- [ ] Security headers on every response (`X-Content-Type-Options: nosniff`, `Content-Security-Policy: default-src
  'none'; frame-ancestors 'none'`, HSTS). Then remove the HOS waivers from `baseline.json` (they expire 2027-05-31)
- [ ] `CA2007` decided for the host project, with the reason next to the setting (ADR 0002)
- [ ] One log line per event: source, item, the engine's decision (`SkipReason` or apply). No tokens, no field contents
- [ ] New packages (Functions worker, `Azure.Data.Tables`, `Azure.Identity`) in `docs/dependencies.md`, each used only
  outside the core

**Acceptance:** the same event delivered twice writes once; an older event never overwrites a newer change; the echo of
the sync's own write ends as `EchoSuppressed` or `NoChange`. Proven by tests and, with Phase 2, in the sandbox.

## Phase 2: real systems

- [!] Sandbox: one throwaway Jira project, one throwaway ADO project and a mapping between them. Blocked until the Jira
  site URL and the ADO organization and projects are known; needed only for tests against real systems
- [ ] Record one real delivery per event type (Jira webhook, ADO service hook) and keep it as a fixture without
  personal data. Confirm whether `resource.revision.fields` carries the full state; the ADO parser is defensive until
  then
- [ ] Decision: register the Jira webhook per project or globally (DESIGN.md section 11; depends on the admin rights of
  the person installing it)
- [ ] Deployment example as infrastructure as code: Function app, storage account, Key Vault, managed identity with
  access to the ADO organization; documented in `docs/`
- [ ] Repeatable end-to-end run in the sandbox for both directions, the echo case and a duplicate delivery
- [ ] SBOM for the released Function package (baseline rule SEC-002)
- [ ] Release 0.1.0

## Phase 3: breadth

Each item brings its configuration, schema and loader validation along; nothing is configured before it works.

- [ ] Create the item on the other side when no pair exists: issue type and work item type per mapping, area path from
  the mapping, the new pair stored
- [ ] Assignee: a user mapping from Jira `accountId` to ADO identity in the configuration. Until then the parsers
  deliver no assignee
- [ ] Comments in both directions (DESIGN.md lists `comment_created`), echo-safe like the fields
- [ ] Links: `linkTypeMap` from Jira link types to ADO link types; unmapped links are skipped (DESIGN.md section 8)
- [ ] Iterations: Jira sprint to ADO iteration. Decision: by name or by a table in the configuration (DESIGN.md
  section 11)

## Phase 4: conflict policies

DESIGN.md section 7. v1 is `lastWriteWins`.

- [ ] `sourceOfTruthPerField`: each field is owned by one side, for example status by ADO and description by Jira
- [ ] `flagForReview`: when both sides changed since the last sync, comment on both items and skip the write

## Maintenance

Ongoing work outside the phases.

- [!] Move the conformance pin in `ci.yml` to the first engineering-baseline release with the C# rules, and lift the
  Dependabot ignore for it. Blocked: the latest release is still v0.1.1, which lacks `CSH-007..018` and the `GEN-014`
  fix for NuGet lock files
- [ ] Before 2027-05-31: the HOS waivers in `baseline.json` expire. Replace them with the security headers of Phase 1,
  or with an API-host variant of the rules in engineering-baseline

## Later

| Could | Note |
| --- | --- |
| YAML as a second configuration format | Must map to the same model; needs a YAML library (entry in `docs/dependencies.md`) |
| Attachments | Non-goal for now (README) |
| Custom field mapping | Non-goal for now (README) |

## Won't

| Not planned | Why |
| --- | --- |
| Jira Data Center / Server | Jira Cloud only (README, non-goals) |
| Syncing repositories, pipelines or boards | Out of scope: the sync is about work items |
| Secrets in the configuration file | The configuration names the setting; values come from app settings or Key Vault |

## Open decisions

| Decision | Needed for | Source |
| --- | --- | --- |
| Inline processing or a queue | Phase 1 | DESIGN.md, section 3 |
| Retry strategy and limits | Phase 1 | DESIGN.md, section 11 |
| Webhook authentication per source | Phase 1 | DESIGN.md, section 9 |
| JSON Schema check in tests: which package, if any | Phase 1 | Loader and schema can drift |
| Sandbox systems: Jira site, ADO organization and projects | Phase 2 | Needed for tests against real systems |
| Jira webhook per project or global | Phase 2 | DESIGN.md, section 11 |
| Iteration mapping by name or by table | Phase 3 | DESIGN.md, section 11 |
