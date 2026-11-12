# Contributing to jira-ado-sync

Thank you for taking the time to contribute. This document describes how to propose changes and what we expect from a
pull request. The quality rules follow [engineering-baseline](https://github.com/miralem-cebic/engineering-baseline);
the working agreement is in [CLAUDE.md](CLAUDE.md).

By participating in this project you agree to abide by our [Code of Conduct](CODE_OF_CONDUCT.md).

## Ways to contribute

- **Report bugs** using the [bug report template](https://github.com/miralem-cebic/jira-ado-sync/issues/new?template=bug_report.yml).
- **Suggest features** using the [feature request template](https://github.com/miralem-cebic/jira-ado-sync/issues/new?template=feature_request.yml).
- **Improve documentation**, typos and unclear wording included.
- **Submit code** via pull requests.

For security issues, do not open a public issue. See [SECURITY.md](SECURITY.md).

## Prerequisites

- The .NET SDK named in [global.json](global.json) (.NET 10).
- Node 22 or later, only to run the baseline conformance check locally.

## Workflow

`main` is protected. Every change reaches it through a pull request:

1. **Discuss first** for anything larger than a small fix. Open an issue or comment on an existing one.
2. **Create a branch** with a descriptive name, for example `feat/comment-sync` or `fix/ado-rev-parsing`.
3. **Write the test first.** Watch it fail for the right reason, then implement. A bug fix starts with a test that
   reproduces the bug.
4. **Run the gate:** `scripts/verify.sh` must be green (locked restore, build with zero warnings, tests,
   `dotnet format --verify-no-changes`).
5. **Update `CHANGELOG.md`** under `[Unreleased]`, and the documentation the change touches.
6. **Open a pull request** against `main` and fill in the template.
7. **Pass all checks.** `verify`, `pr-title` and `conformance` must be green.
8. **Merge.** We use squash merges, so the pull request title becomes the commit message on `main`.

Keep pull requests small: one topic per pull request.

## Commit messages

We follow [Conventional Commits](https://www.conventionalcommits.org/). The pull request title must follow the same
format, because it becomes the commit message after a squash merge and is checked in CI.

```text
feat(jira): sync comments in both directions
fix(ado): read the revision from resource.revision when rev is missing
docs(adr): record the retry strategy for rate limits
```

Allowed types: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`.

## Architecture decisions

A decision that affects the architecture, a dependency or security gets an ADR in [docs/adr/](docs/adr/README.md).
Accepted ADRs are not edited; a new ADR supersedes them.

## Dependencies

A new package needs an entry in [docs/dependencies.md](docs/dependencies.md) with its reason and the platform
alternative that was considered. Versions live only in `Directory.Packages.props`; commit the updated
`packages.lock.json` files.

## Releases

A release is a `v*` tag on `main`, for example `v0.1.0`. The release workflow runs the gate again and creates the
GitHub release with notes generated from the merged pull requests. Move the `[Unreleased]` entries of `CHANGELOG.md`
under the new version in the pull request before tagging.

## Licence

By contributing, you agree that your contributions are licensed under the [MIT licence](LICENSE) of this project.
