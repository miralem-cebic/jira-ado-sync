using JiraAdoSync.Core.Model;

namespace JiraAdoSync.Core.Delivery;

/// <summary>
/// A received delivery, as the host passes it on. The host reads the headers. The receiver decides which of them it needs.
/// </summary>
/// <param name="Source">The endpoint the delivery arrived at.</param>
/// <param name="Body">The body exactly as received.</param>
/// <param name="SignatureHeader">The <c>X-Hub-Signature</c> header of a Jira delivery, or null.</param>
/// <param name="AuthorizationHeader">The <c>Authorization</c> header of an ADO delivery, or null.</param>
public sealed record WebhookRequest(Side Source, ReadOnlyMemory<byte> Body, string? SignatureHeader, string? AuthorizationHeader);

/// <summary>The credentials the receiver checks deliveries against. Values come from settings, never from the configuration file.</summary>
/// <param name="JiraSecret">The secret of the Jira webhook.</param>
/// <param name="AdoUserName">The user name configured in the ADO service hook.</param>
/// <param name="AdoPassword">The password configured in the ADO service hook.</param>
public sealed record WebhookSecrets(string JiraSecret, string AdoUserName, string AdoPassword);

/// <summary>
/// The identities the sync writes as. The processor treats a change made by one of them as an echo of its own write.
/// </summary>
/// <param name="JiraServiceAccountId">The accountId of the Jira service account.</param>
/// <param name="AdoServiceIdentity">The unique name of the ADO identity the sync writes as.</param>
public sealed record SyncIdentities(string JiraServiceAccountId, string AdoServiceIdentity);
