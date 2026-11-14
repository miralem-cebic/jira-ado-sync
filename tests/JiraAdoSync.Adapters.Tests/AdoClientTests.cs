using System.Net;
using System.Text.Json.Nodes;
using JiraAdoSync.Core.Config;
using JiraAdoSync.Core.Model;
using static JiraAdoSync.Adapters.Tests.StubHttpHandler;

namespace JiraAdoSync.Adapters.Tests;

public class AdoClientTests
{
    private static (AdoClient Client, StubHttpHandler Stub) Create()
    {
        var stub = new StubHttpHandler();
        var http = new HttpClient(stub) { BaseAddress = new Uri("https://dev.example.test/") };
        return (new AdoClient(http), stub);
    }

    private static ProjectMapping Mapping(string adoProject = "Project") => new()
    {
        Id = "proj",
        JiraProject = "PROJ",
        AdoProject = adoProject,
        AreaPath = "Project\\Team",
        StatusMap = new Dictionary<string, string>
        {
            ["In Progress"] = "Active",
            ["Done"] = "Closed",
        },
    };

    [Fact]
    public async Task PatchCarriesTheRevisionThatWasReadFirst()
    {
        var (client, stub) = Create();
        stub.Enqueue(Reply(HttpStatusCode.OK, "{\"id\":42,\"rev\":7}"));
        stub.Enqueue(Reply(HttpStatusCode.OK, "{}"));

        await client.ApplyAsync("42", new CanonicalItem("Title", null, "In Progress", null), Mapping(), CancellationToken.None);

        Assert.Equal(2, stub.Requests.Count);
        var read = stub.Requests[0];
        Assert.Equal(HttpMethod.Get, read.Method);
        Assert.Equal("/Project/_apis/wit/workitems/42?api-version=7.1", read.PathAndQuery);

        var patch = stub.Requests[1];
        Assert.Equal(HttpMethod.Patch, patch.Method);
        Assert.Equal("/Project/_apis/wit/workitems/42?api-version=7.1", patch.PathAndQuery);
        Assert.Equal("application/json-patch+json", patch.ContentType);

        var operations = JsonNode.Parse(patch.Body!)!.AsArray();
        Assert.Equal("test", operations[0]!["op"]!.GetValue<string>());
        Assert.Equal("/rev", operations[0]!["path"]!.GetValue<string>());
        Assert.Equal(7, operations[0]!["value"]!.GetValue<int>());
        Assert.Contains(operations, operation =>
            operation!["path"]!.GetValue<string>() == "/fields/System.State"
            && operation["value"]!.GetValue<string>() == "Active");
    }

    [Fact]
    public async Task ProjectNameIsEscapedInThePath()
    {
        var (client, stub) = Create();
        stub.Enqueue(Reply(HttpStatusCode.OK, "{\"id\":42,\"rev\":1}"));
        stub.Enqueue(Reply(HttpStatusCode.OK, "{}"));

        await client.ApplyAsync("42", new CanonicalItem("Title", null, null, null), Mapping("My Project"), CancellationToken.None);

        Assert.Equal("/My%20Project/_apis/wit/workitems/42?api-version=7.1", stub.Requests[0].PathAndQuery);
    }

    [Fact]
    public async Task ItemWithoutAnyFieldSendsNothing()
    {
        var (client, stub) = Create();

        await client.ApplyAsync("42", new CanonicalItem(null, null, null, null), Mapping(), CancellationToken.None);

        Assert.Empty(stub.Requests);
    }

    [Fact]
    public async Task RejectedPatchThrowsSoTheQueueRetries()
    {
        var (client, stub) = Create();
        stub.Enqueue(Reply(HttpStatusCode.OK, "{\"id\":42,\"rev\":7}"));
        stub.Enqueue(Reply(HttpStatusCode.Conflict));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.ApplyAsync("42", new CanonicalItem("Title", null, null, null), Mapping(), CancellationToken.None));
    }
}
