#!/usr/bin/env bash
# Applies the repository settings the baseline expects (rules REPO-001..009).
# Needs the GitHub CLI logged in with admin rights on the repository.
#
#   scripts/apply-repo-settings.sh owner/name "verify,pr-title,conformance"
#
# The second argument lists the CI job names that must pass before merging.
set -euo pipefail

repo="${1:?usage: apply-repo-settings.sh owner/name check1,check2}"
checks="${2:?usage: apply-repo-settings.sh owner/name check1,check2}"
branch="$(gh api "repos/$repo" --jq .default_branch)"

echo "Merge settings, branch cleanup, secret scanning"
gh api -X PATCH "repos/$repo" \
  -F allow_squash_merge=true -F allow_merge_commit=false -F allow_rebase_merge=false \
  -F delete_branch_on_merge=true \
  -f squash_merge_commit_title=PR_TITLE -f squash_merge_commit_message=PR_BODY \
  -f 'security_and_analysis[secret_scanning][status]=enabled' \
  -f 'security_and_analysis[secret_scanning_push_protection][status]=enabled' >/dev/null

echo "Dependabot alerts and security updates"
gh api -X PUT "repos/$repo/vulnerability-alerts" >/dev/null
gh api -X PUT "repos/$repo/automated-security-fixes" >/dev/null

echo "Default workflow token: read-only"
gh api -X PUT "repos/$repo/actions/permissions/workflow" \
  -f default_workflow_permissions=read -F can_approve_pull_request_reviews=false >/dev/null

echo "Protect $branch"
contexts="$(printf '%s' "$checks" | jq -R 'split(",")')"
jq -n --argjson contexts "$contexts" '{
  required_status_checks: { strict: true, contexts: $contexts },
  enforce_admins: true,
  required_pull_request_reviews: { required_approving_review_count: 0 },
  restrictions: null,
  allow_force_pushes: false,
  allow_deletions: false
}' | gh api -X PUT "repos/$repo/branches/$branch/protection" --input - >/dev/null

echo "Done. Verify with: GITHUB_TOKEN=... npx github:miralem-cebic/engineering-baseline check"
