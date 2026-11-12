#!/usr/bin/env bash
# The local gate. CI (job `verify`) and the release workflow run exactly this script.
#
#   scripts/verify.sh
#
# Needs the .NET SDK from global.json. CI also runs ShellCheck and the vulnerable package report.
set -euo pipefail

cd "$(dirname "$0")/.."
solution=JiraAdoSync.slnx

echo "Restore from the lock files"
dotnet restore "$solution" --locked-mode

echo "Build: every warning is an error"
dotnet build "$solution" --no-restore -c Release

echo "Tests"
dotnet test --solution "$solution" --no-build -c Release

echo "Formatting"
dotnet format "$solution" --no-restore --verify-no-changes

echo "All gates passed."
