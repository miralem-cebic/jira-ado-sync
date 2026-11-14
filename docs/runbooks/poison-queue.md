# Runbook: messages in the poison queue

Applies to ADR 0010. The delivery queue is `sync-deliveries`. A message that fails for the tenth time moves to
`sync-deliveries-poison`.

## Symptoms

- The poison queue holds messages. Its length is in the storage account. The alert on it is a Phase 2 item.
- The host log shows `Message has reached MaxDequeueCount` for the message.

## 1. Find the cause before the replay

- The processor logs every attempt with the delivery's blob name: `Delivery <Source> item <ItemId> (<BlobName>) ...`. A
  retry because of a lost save race is logged as a warning.
- Typical causes: Jira or Azure DevOps unavailable, or rate limited for longer than the window; a credential that has
  expired; a project missing from the configuration; a Jira workflow without a direct transition (roadmap, Phase 3).
- Fix the cause first. A replay of a message whose cause still exists ends in the poison queue again.

## 2. Check that the bodies still exist

A message carries its source and its blob name, for example `{"source":"jira","blobName":"jira/<sha256>"}`. The body is
in the `raw-deliveries` container under that name. A message whose body is gone cannot be replayed. Its change has to be
synced by hand, and the message stays in the poison queue until then.

## 3. Replay

    python3 -m venv .venv-replay && .venv-replay/bin/pip install -r scripts/requirements-replay.txt
    .venv-replay/bin/python scripts/replay-poison.py --connection-string "$AZURE_STORAGE_CONNECTION" --dry-run
    .venv-replay/bin/python scripts/replay-poison.py --connection-string "$AZURE_STORAGE_CONNECTION"

The script lists the messages of the poison queue. A message whose body is missing stays in the poison queue and is
reported. Every other message is sent to `sync-deliveries`, and then it is deleted from the poison queue. The processor
handles a replayed message like a new delivery. The processing is idempotent, so a message that was already applied is
skipped.

Do not delete a poison message without a replay decision.

## 4. Verify

The host log shows `processed` or `dropped` for each replayed message. The poison queue is empty when the replay is
complete.
