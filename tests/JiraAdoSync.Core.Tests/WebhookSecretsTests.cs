using JiraAdoSync.Core.Delivery;

namespace JiraAdoSync.Core.Tests;

public class WebhookSecretsTests
{
    [Fact]
    public void ToStringHidesEveryValue()
    {
        var secrets = new WebhookSecrets("jira-secret-value", "hook-user-value", "hook-password-value");

        var text = secrets.ToString();

        Assert.DoesNotContain("jira-secret-value", text, StringComparison.Ordinal);
        Assert.DoesNotContain("hook-user-value", text, StringComparison.Ordinal);
        Assert.DoesNotContain("hook-password-value", text, StringComparison.Ordinal);
    }
}
