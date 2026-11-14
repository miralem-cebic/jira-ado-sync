using System.Net;
using System.Text;
using static JiraAdoSync.Adapters.Tests.StubHttpHandler;

namespace JiraAdoSync.Adapters.Tests;

public class BasicAuthorizationHandlerTests
{
    [Fact]
    public async Task SendsUserNameAndSecretAsBasicAuthorization()
    {
        var stub = new StubHttpHandler();
        stub.Enqueue(Reply(HttpStatusCode.OK, "{}"));
        var handler = new BasicAuthorizationHandler("sync@example.test", "api-token") { InnerHandler = stub };
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://jira.example.test/") };

        using var response = await client.GetAsync(new Uri("rest/api/3/myself", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("sync@example.test:api-token")), stub.Requests[0].Authorization);
    }

    [Fact]
    public async Task PersonalAccessTokenIsSentWithAnEmptyUserName()
    {
        var stub = new StubHttpHandler();
        stub.Enqueue(Reply(HttpStatusCode.OK, "{}"));
        var handler = new BasicAuthorizationHandler(string.Empty, "pat-value") { InnerHandler = stub };
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://dev.example.test/") };

        using var response = await client.GetAsync(new Uri("Project/_apis/wit/workitems/1", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(":pat-value")), stub.Requests[0].Authorization);
    }
}
