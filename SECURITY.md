# Security policy

## Supported versions

There is no release yet. Once there is one, security fixes are provided for the latest release only.

| Version        | Supported          |
| -------------- | ------------------ |
| Latest release | :white_check_mark: |
| Older releases | :x:                |

## Reporting a vulnerability

**Please do not report security vulnerabilities through public GitHub issues, discussions, or pull requests.**

Report them privately using GitHub's private vulnerability reporting:

1. Go to the [Security tab](https://github.com/miralem-cebic/jira-ado-sync/security) of this repository.
2. Click **Report a vulnerability**.
3. Describe the issue, the steps to reproduce it, and the potential impact.

Please include as much of the following as possible:

- The version or commit you tested.
- A description of the vulnerability and its impact.
- Steps to reproduce, or a proof of concept. Use invented test data, never real work items, tokens or account IDs.
- Any suggested mitigation or fix.

## What to expect

- **Acknowledgement** within 7 days.
- **Assessment** of severity and impact, shared with you in the advisory.
- **Fix and release** as soon as practical. We will coordinate the disclosure date with you.
- **Credit** in the release notes and the security advisory, unless you prefer to remain anonymous.

## Scope

In scope: the code, configuration schema and pipeline in this repository. Of particular interest:

- requests that the webhook endpoints accept without a valid secret, or that make the sync write as its service
  identity on behalf of someone else;
- ways to leak the Jira API token, the ADO token or the managed identity's access into logs, responses or the other
  system;
- payloads that make the sync loop (writes that trigger further writes) or overwrite a newer change;
- content from one side (HTML, ADF) that ends up unencoded or executable on the other side.

Out of scope: vulnerabilities in Jira Cloud, Azure DevOps or Azure itself, and issues that require a compromised
Azure subscription, Jira site or ADO organization.
