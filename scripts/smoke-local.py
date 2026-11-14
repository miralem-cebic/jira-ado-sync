#!/usr/bin/env python3
"""
Sends the webhook requests of docs/running-locally.md to a running host and checks the answers (ADR 0007, ADR 0009).

It uses only the standard library. The secrets come from the environment, so that they stay out of the command line:

    JIRA_WEBHOOK_SECRET=... ADO_WEBHOOK_USER=... ADO_WEBHOOK_PASSWORD=... python3 scripts/smoke-local.py

Start the host first (func start --port 7071). The exit code is 0 when every answer is the expected one.
"""

import argparse
import base64
import hashlib
import hmac
import json
import os
import sys
import urllib.error
import urllib.request

SECURITY_HEADERS = {
    "Content-Security-Policy": "default-src 'none'; frame-ancestors 'none'",
    "X-Content-Type-Options": "nosniff",
    "Cache-Control": "no-store",
    "Strict-Transport-Security": "max-age=31536000",
}
MAX_BODY_BYTES = 2 * 1024 * 1024


def jira_body(account: str, key: str = "PROJ-1", event: str = "jira:issue_updated") -> bytes:
    return json.dumps({
        "webhookEvent": event,
        "timestamp": 1767261600000,
        "user": {"accountId": account},
        "issue": {"key": key, "fields": {"summary": "Smoke test title", "status": {"name": "In Progress"}, "project": {"key": "PROJ"}}},
    }, separators=(",", ":")).encode()


def ado_body() -> bytes:
    return json.dumps({
        "eventType": "workitem.updated",
        "resource": {
            "workItemId": 42,
            "rev": 8,
            "revisedBy": {"uniqueName": "someone@example.test"},
            "revision": {"fields": {
                "System.TeamProject": "your-ado-project",
                "System.ChangedDate": "2026-01-01T10:05:00Z",
                "System.Title": "Smoke test from ADO",
                "System.State": "Closed",
            }},
        },
    }, separators=(",", ":")).encode()


def sign(body: bytes, secret: str) -> str:
    return "sha256=" + hmac.new(secret.encode(), body, hashlib.sha256).hexdigest()


def basic(user: str, password: str) -> str:
    return "Basic " + base64.b64encode(f"{user}:{password}".encode()).decode()


def post(base: str, path: str, body: bytes, headers: dict) -> tuple[int, dict]:
    request = urllib.request.Request(
        f"{base}/api/webhooks/{path}", data=body, method="POST",
        headers={"Content-Type": "application/json", **headers},
    )
    try:
        with urllib.request.urlopen(request, timeout=60) as response:
            return response.status, dict(response.headers)
    except urllib.error.HTTPError as error:
        return error.code, dict(error.headers)


def main() -> int:
    parser = argparse.ArgumentParser(description="Smoke test of the webhook endpoints of a local host.")
    parser.add_argument("--base", default="http://localhost:7071", help="Base address of the host.")
    parser.add_argument("--service-account-id", default="your-jira-service-account-id",
                        help="accountId of the sync's Jira service account, as in the configuration.")
    args = parser.parse_args()

    jira_secret = os.environ.get("JIRA_WEBHOOK_SECRET")
    ado_user = os.environ.get("ADO_WEBHOOK_USER")
    ado_password = os.environ.get("ADO_WEBHOOK_PASSWORD")
    if not (jira_secret and ado_user and ado_password):
        print("Set JIRA_WEBHOOK_SECRET, ADO_WEBHOOK_USER and ADO_WEBHOOK_PASSWORD.", file=sys.stderr)
        return 2

    jira = jira_body("someone-else")
    ado = ado_body()
    cases = [
        ("Jira, valid signature", "jira", jira, {"X-Hub-Signature": sign(jira, jira_secret)}, 202),
        ("Jira, wrong secret", "jira", jira, {"X-Hub-Signature": sign(jira, "wrong")}, 401),
        ("Jira, no signature header", "jira", jira, {}, 401),
        ("Jira, valid signature, body is not JSON", "jira", b"not json",
         {"X-Hub-Signature": sign(b"not json", jira_secret)}, 400),
        ("Jira, same delivery again", "jira", jira, {"X-Hub-Signature": sign(jira, jira_secret)}, 202),
        ("Jira, event type the sync ignores", "jira",
         json.dumps({"webhookEvent": "comment_created", "comment": {"id": "1"}}).encode(),
         {"X-Hub-Signature": sign(json.dumps({"webhookEvent": "comment_created", "comment": {"id": "1"}}).encode(), jira_secret)}, 202),
        ("Jira, change by the sync's own account (echo)", "jira", jira_body(args.service_account_id),
         {"X-Hub-Signature": sign(jira_body(args.service_account_id), jira_secret)}, 202),
        ("Jira, body over the 2 MiB limit", "jira", b"x" * (MAX_BODY_BYTES + 1), {}, 413),
        ("ADO, valid basic credentials", "ado", ado, {"Authorization": basic(ado_user, ado_password)}, 202),
        ("ADO, wrong password", "ado", ado, {"Authorization": basic(ado_user, "wrong")}, 401),
    ]

    failures = 0
    for index, (name, path, body, headers, expected) in enumerate(cases):
        status, response_headers = post(args.base, path, body, headers)
        ok = status == expected
        if index == 0:
            missing = [key for key, value in SECURITY_HEADERS.items() if response_headers.get(key) != value]
            if missing:
                ok = False
                print(f"     security headers missing or different: {', '.join(missing)}")
        failures += 0 if ok else 1
        print(f"{'OK  ' if ok else 'FAIL'} {status} (expected {expected}): {name}")

    print(f"failures: {failures}")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
