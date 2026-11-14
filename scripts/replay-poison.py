#!/usr/bin/env python3
"""
Moves the messages of the poison queue back to the delivery queue (ADR 0010, docs/runbooks/poison-queue.md).

A message whose raw body is missing in the raw-deliveries container cannot be replayed. It stays in the poison queue and
is reported. The messages are read with the same Base64 encoding that the Functions queue trigger uses.

Usage:
    python3 scripts/replay-poison.py --connection-string "<storage connection string>" [--dry-run] [--limit 32]
"""

import argparse
import json
import sys

from azure.storage.blob import BlobClient
from azure.storage.queue import QueueClient, TextBase64DecodePolicy, TextBase64EncodePolicy

POISON_QUEUE = "sync-deliveries-poison"
DELIVERY_QUEUE = "sync-deliveries"
RAW_CONTAINER = "raw-deliveries"


def queue(connection_string: str, name: str) -> QueueClient:
    return QueueClient.from_connection_string(
        connection_string,
        name,
        message_encode_policy=TextBase64EncodePolicy(),
        message_decode_policy=TextBase64DecodePolicy(),
    )


def body_exists(connection_string: str, blob_name: str) -> bool:
    blob = BlobClient.from_connection_string(connection_string, RAW_CONTAINER, blob_name)
    return blob.exists()


def main() -> int:
    parser = argparse.ArgumentParser(description="Replay the poison queue of the delivery queue (ADR 0010).")
    parser.add_argument("--connection-string", required=True, help="Storage connection string. Keep it out of shell history.")
    parser.add_argument("--dry-run", action="store_true", help="List the messages and their bodies, change nothing.")
    parser.add_argument("--limit", type=int, default=32, help="Most messages to handle in one run (1 to 32).")
    args = parser.parse_args()
    if not 1 <= args.limit <= 32:
        parser.error("--limit must be between 1 and 32")

    poison = queue(args.connection_string, POISON_QUEUE)
    delivery = queue(args.connection_string, DELIVERY_QUEUE)

    # A dry run only peeks. Receiving would hide the messages for the visibility timeout, and a real run started right
    # after would find nothing.
    if args.dry_run:
        messages = poison.peek_messages(max_messages=args.limit)
    else:
        messages = poison.receive_messages(max_messages=args.limit)

    replayed = 0
    missing = 0
    for message in messages:
        try:
            payload = json.loads(message.content)
            blob_name = payload["blobName"]
            source = payload["source"]
        except (TypeError, KeyError, ValueError):
            print(f"unreadable message {message.id}: left in the poison queue")
            missing += 1
            continue

        if not body_exists(args.connection_string, blob_name):
            print(f"{source} {blob_name}: body is gone, left in the poison queue")
            missing += 1
            continue

        if args.dry_run:
            print(f"{source} {blob_name}: would be replayed")
            continue

        delivery.send_message(message.content)
        poison.delete_message(message.id, message.pop_receipt)
        print(f"{source} {blob_name}: replayed")
        replayed += 1

    print(f"replayed {replayed}, left {missing} in the poison queue" + (" (dry run)" if args.dry_run else ""))
    return 1 if missing else 0


if __name__ == "__main__":
    sys.exit(main())
