using System.Net;
using System.Text.Json.Nodes;
using JiraAdoSync.Core.Model;
using static JiraAdoSync.Adapters.Tests.StubHttpHandler;

namespace JiraAdoSync.Adapters.Tests;

public class JiraClientTests
{
    private const string Transitions =
        "{\"transitions\":[{\"id\":\"11\",\"to\":{\"name\":\"To Do\"}},{\"id\":\"31\",\"to\":{\"name\":\"Done\"}}]}";

    private static (JiraClient Client, StubHttpHandler Stub) Create()
    {
        var stub = new StubHttpHandler();
        var http = new HttpClient(stub) { BaseAddress = new Uri("https://jira.example.test/") };
        return (new JiraClient(http), stub);
    }

    [Fact]
    public async Task TitleAndDescriptionAreWrittenWithOnePut()
    {
        var (client, stub) = Create();
        stub.Enqueue(Reply(HttpStatusCode.NoContent));

        await client.ApplyAsync("PROJ-1", new CanonicalItem("New title", "New description", null, null), CancellationToken.None);

        var request = Assert.Single(stub.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/rest/api/3/issue/PROJ-1", request.PathAndQuery);
        var fields = JsonNode.Parse(request.Body!)!["fields"]!;
        Assert.Equal("New title", fields["summary"]!.GetValue<string>());
        Assert.NotNull(fields["description"]);
    }

    [Fact]
    public async Task StatusChangeUsesTheTransitionThatLeadsToIt()
    {
        var (client, stub) = Create();
        stub.Enqueue(Reply(HttpStatusCode.OK, "{\"fields\":{\"status\":{\"name\":\"In Progress\"}}}"));
        stub.Enqueue(Reply(HttpStatusCode.OK, Transitions));
        stub.Enqueue(Reply(HttpStatusCode.NoContent));

        await client.ApplyAsync("PROJ-1", new CanonicalItem(null, null, "Done", null), CancellationToken.None);

        Assert.Equal(3, stub.Requests.Count);
        Assert.Equal("/rest/api/3/issue/PROJ-1?fields=status", stub.Requests[0].PathAndQuery);
        Assert.Equal("/rest/api/3/issue/PROJ-1/transitions", stub.Requests[1].PathAndQuery);
        var post = stub.Requests[2];
        Assert.Equal(HttpMethod.Post, post.Method);
        Assert.Equal("/rest/api/3/issue/PROJ-1/transitions", post.PathAndQuery);
        Assert.Equal("31", JsonNode.Parse(post.Body!)!["transition"]!["id"]!.GetValue<string>());
    }

    [Fact]
    public async Task IssueAlreadyInTargetStatusIsNotTransitioned()
    {
        var (client, stub) = Create();
        stub.Enqueue(Reply(HttpStatusCode.OK, "{\"fields\":{\"status\":{\"name\":\"Done\"}}}"));

        await client.ApplyAsync("PROJ-1", new CanonicalItem(null, null, "Done", null), CancellationToken.None);

        Assert.Single(stub.Requests);
    }

    [Fact]
    public async Task StatusWithoutTransitionFromCurrentStateThrowsAndDoesNotTransition()
    {
        var (client, stub) = Create();
        stub.Enqueue(Reply(HttpStatusCode.OK, "{\"fields\":{\"status\":{\"name\":\"To Do\"}}}"));
        stub.Enqueue(Reply(HttpStatusCode.OK, "{\"transitions\":[]}"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.ApplyAsync("PROJ-1", new CanonicalItem(null, null, "Done", null), CancellationToken.None));

        Assert.DoesNotContain(stub.Requests, request => request.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task RejectedWriteThrowsSoTheQueueRetries()
    {
        var (client, stub) = Create();
        stub.Enqueue(Reply(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.ApplyAsync("PROJ-1", new CanonicalItem("Title", null, null, null), CancellationToken.None));
    }
}
