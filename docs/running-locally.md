# Running the Function locally

The Function runs on the Azure Functions Core Tools, with Azurite as the storage. The webhook endpoints and the processor
run end to end: a signed delivery is authenticated, stored, queued and processed. Jira and Azure DevOps are only contacted
for items that are linked in the `SyncItems` table. Without a link, the change is skipped as `NotLinked`.

## Prerequisites

- The .NET SDK from `global.json`
- Node.js 22 or later, for the two tools below

## Steps

1. Install the tools: `npm install -g azurite@3 azure-functions-core-tools@4`.
2. Start the storage emulator: `azurite --skipApiVersionCheck --location ./azurite-data`. The flag is needed because the
   current Azure SDK sends an API version that Azurite 3 does not accept.
3. Publish the app: `dotnet publish src/JiraAdoSync.Functions -c Release -o ./local-app`.
4. In `./local-app`, create `config.json`, starting from `config/config.example.json`, and `local.settings.json`:

   ```json
   {
     "IsEncrypted": false,
     "Values": {
       "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
       "AzureWebJobsStorage": "UseDevelopmentStorage=true",
       "JIRA_API_TOKEN": "local-jira-token",
       "JIRA_WEBHOOK_SECRET": "local-jira-secret",
       "ADO_WEBHOOK_USER": "hook-user",
       "ADO_WEBHOOK_PASSWORD": "local-hook-password"
     }
   }
   ```

   The values are invented for a local run. `local.settings.json` is in `.gitignore`. Keep it out of the repository.

5. Start the host in `./local-app`: `func start --port 7071`. The host creates the container, the queue and the table at
   startup. If a setting is missing, it stops with a message that names it.
6. Send a signed Jira delivery to `POST http://localhost:7071/api/webhooks/jira`, with the header
   `X-Hub-Signature: sha256=<hex HMAC-SHA256 of the body, keyed with JIRA_WEBHOOK_SECRET>`. The answer is `202`. Without a
   valid signature it is `401`, and a body that is not JSON gets `400` (ADR 0007).
7. The host log shows the decision, for example `Delivery Jira item PROJ-1 (jira/…) processed: Skipped { Reason = NotLinked }.`

## Tests against the emulator

The storage tests run only when `SYNC_TEST_STORAGE` is set. With Azurite running:

    SYNC_TEST_STORAGE="UseDevelopmentStorage=true" dotnet test --solution JiraAdoSync.slnx -c Release
