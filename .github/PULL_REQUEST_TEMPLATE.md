## What and why

<!-- One or two sentences. Link the issue with "Closes #123". The PR title is a Conventional Commit: feat:, fix:, docs:, chore: ... -->

## How it was tested

<!-- Which tests were added or changed? Anything checked by hand against a real Jira site or ADO organization? -->

## Checklist

- [ ] A failing test came first (a bug fix starts with a test that reproduces the bug)
- [ ] `scripts/verify.sh` is green and its output was read
- [ ] No secret, token or real work item data in the diff, the examples or the tests
- [ ] A change to sync behaviour keeps loop prevention and idempotency intact (`docs/DESIGN.md`, sections 6 and 7)
- [ ] A new dependency has an entry in `docs/dependencies.md`
- [ ] An architecture decision has an ADR in `docs/adr/`
- [ ] `CHANGELOG.md` has an entry under `[Unreleased]`
