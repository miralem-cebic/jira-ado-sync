using System.Text;
using JiraAdoSync.Core.Delivery;
using JiraAdoSync.Core.Model;

namespace JiraAdoSync.Core.Tests;

public class DeliveryKeyTests
{
    // Vector computed outside this code base: SHA-256 of the body below, in lower-case hex.
    private const string Body = "{\"webhookEvent\":\"jira:issue_updated\"}";
    private const string BodyDigest = "9a9afda1ae2c17f08394c0bb3faacbc71348b29a3bf53bbd48bdee8fe43b528f";

    [Fact]
    public void KeyIsTheSourceAndTheSha256OfTheRawBody()
    {
        var key = DeliveryKey.For(Side.Jira, Encoding.UTF8.GetBytes(Body));

        Assert.Equal("jira/" + BodyDigest, key);
    }

    [Fact]
    public void SameBodyFromTheOtherSourceHasAnotherKey()
    {
        var key = DeliveryKey.For(Side.Ado, Encoding.UTF8.GetBytes(Body));

        Assert.Equal("ado/" + BodyDigest, key);
    }

    [Fact]
    public void ChangedBodyHasAnotherKey()
    {
        var key = DeliveryKey.For(Side.Jira, Encoding.UTF8.GetBytes(Body + " "));

        Assert.NotEqual("jira/" + BodyDigest, key);
    }
}
