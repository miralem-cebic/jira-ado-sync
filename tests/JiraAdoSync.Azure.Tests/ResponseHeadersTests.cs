namespace JiraAdoSync.Azure.Tests;

public class ResponseHeadersTests
{
    [Fact]
    public void EveryResponseCarriesTheBaselineSecurityHeaders()
    {
        var headers = ResponseHeaders.Security.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        Assert.Equal("default-src 'none'; frame-ancestors 'none'", headers["Content-Security-Policy"]);
        Assert.Equal("nosniff", headers["X-Content-Type-Options"]);
        Assert.Equal("no-store", headers["Cache-Control"]);
        Assert.Equal("max-age=31536000", headers["Strict-Transport-Security"]);
    }
}
