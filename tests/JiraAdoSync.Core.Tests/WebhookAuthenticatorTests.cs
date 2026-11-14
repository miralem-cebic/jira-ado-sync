using System.Text;
using JiraAdoSync.Core.Webhooks;

namespace JiraAdoSync.Core.Tests;

public class WebhookAuthenticatorTests
{
    // Vector computed outside this code base: HMAC-SHA256 of the body below with the key "test-secret".
    private const string JiraBody = "{\"webhookEvent\":\"jira:issue_updated\"}";
    private const string JiraSignature = "sha256=6a19a03252f2ec3f588ec9754e36a01c7a79f36c01f8e1802252412baecb4a13";

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    private static string BasicHeader(string user, string password) =>
        "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(user + ":" + password));

    [Fact]
    public void JiraSignatureOfTheRawBodyIsAccepted()
    {
        Assert.True(WebhookAuthenticator.IsValidJiraSignature(Bytes(JiraBody), JiraSignature, "test-secret"));
    }

    [Fact]
    public void SignatureOfAnotherBodyIsRejected()
    {
        var tampered = Bytes("{\"webhookEvent\":\"jira:issue_created\"}");

        Assert.False(WebhookAuthenticator.IsValidJiraSignature(tampered, JiraSignature, "test-secret"));
    }

    [Fact]
    public void SignatureMadeWithAnotherSecretIsRejected()
    {
        Assert.False(WebhookAuthenticator.IsValidJiraSignature(Bytes(JiraBody), JiraSignature, "other-secret"));
    }

    [Fact]
    public void MissingJiraSignatureIsRejected()
    {
        Assert.False(WebhookAuthenticator.IsValidJiraSignature(Bytes(JiraBody), null, "test-secret"));
    }

    [Fact]
    public void SignatureWithAnotherMethodPrefixIsRejected()
    {
        var sha1 = JiraSignature.Replace("sha256=", "sha1=", StringComparison.Ordinal);

        Assert.False(WebhookAuthenticator.IsValidJiraSignature(Bytes(JiraBody), sha1, "test-secret"));
    }

    [Fact]
    public void SignatureThatIsNotHexIsRejected()
    {
        Assert.False(WebhookAuthenticator.IsValidJiraSignature(Bytes(JiraBody), "sha256=not-hex", "test-secret"));
    }

    [Fact]
    public void TruncatedJiraSignatureIsRejected()
    {
        var truncated = JiraSignature[..^2];

        Assert.False(WebhookAuthenticator.IsValidJiraSignature(Bytes(JiraBody), truncated, "test-secret"));
    }

    [Fact]
    public void EmptySecretIsAConfigurationErrorAndThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            WebhookAuthenticator.IsValidJiraSignature(Bytes(JiraBody), JiraSignature, string.Empty));
    }

    [Fact]
    public void BasicCredentialsThatMatchAreAccepted()
    {
        Assert.True(WebhookAuthenticator.IsValidBasicAuthorization(
            BasicHeader("sync-user", "s3cret"), "sync-user", "s3cret"));
    }

    [Fact]
    public void WrongPasswordIsRejected()
    {
        Assert.False(WebhookAuthenticator.IsValidBasicAuthorization(
            BasicHeader("sync-user", "wrong"), "sync-user", "s3cret"));
    }

    [Fact]
    public void WrongUserNameIsRejected()
    {
        Assert.False(WebhookAuthenticator.IsValidBasicAuthorization(
            BasicHeader("someone-else", "s3cret"), "sync-user", "s3cret"));
    }

    [Fact]
    public void MissingAuthorizationIsRejected()
    {
        Assert.False(WebhookAuthenticator.IsValidBasicAuthorization(null, "sync-user", "s3cret"));
    }

    [Fact]
    public void OtherSchemeIsRejected()
    {
        Assert.False(WebhookAuthenticator.IsValidBasicAuthorization("Bearer abc", "sync-user", "s3cret"));
    }

    [Fact]
    public void SchemeNameIsCaseInsensitive()
    {
        var header = BasicHeader("sync-user", "s3cret").Replace("Basic ", "basic ", StringComparison.Ordinal);

        Assert.True(WebhookAuthenticator.IsValidBasicAuthorization(header, "sync-user", "s3cret"));
    }

    [Fact]
    public void InvalidBase64IsRejected()
    {
        Assert.False(WebhookAuthenticator.IsValidBasicAuthorization("Basic %%%", "sync-user", "s3cret"));
    }

    [Fact]
    public void CredentialsWithoutColonAreRejected()
    {
        var header = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("nocolon"));

        Assert.False(WebhookAuthenticator.IsValidBasicAuthorization(header, "nocolon", "s3cret"));
    }

    [Fact]
    public void PasswordMayContainColons()
    {
        Assert.True(WebhookAuthenticator.IsValidBasicAuthorization(
            BasicHeader("sync-user", "pa:ss:word"), "sync-user", "pa:ss:word"));
    }
}
