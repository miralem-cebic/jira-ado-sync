using System.Text;

namespace JiraAdoSync.Azure.Tests;

public class RequestBodyReaderTests
{
    [Fact]
    public async Task BodyWithinTheLimitIsReturnedUnchanged()
    {
        var bytes = Encoding.UTF8.GetBytes("{\"a\":1}");

        var read = await RequestBodyReader.ReadAsync(new MemoryStream(bytes), 100, TestContext.Current.CancellationToken);

        Assert.Equal(bytes, read);
    }

    [Fact]
    public async Task BodyExactlyAtTheLimitIsAccepted()
    {
        var bytes = new byte[10];

        var read = await RequestBodyReader.ReadAsync(new MemoryStream(bytes), 10, TestContext.Current.CancellationToken);

        Assert.Equal(bytes, read);
    }

    [Fact]
    public async Task BodyOverTheLimitIsRejected()
    {
        var read = await RequestBodyReader.ReadAsync(new MemoryStream(new byte[11]), 10, TestContext.Current.CancellationToken);

        Assert.Null(read);
    }

    [Fact]
    public async Task ReadingStopsAfterTheFirstChunkThatExceedsTheLimit()
    {
        var stream = new MemoryStream(new byte[10 * 1024 * 1024]);

        var read = await RequestBodyReader.ReadAsync(stream, 1024, TestContext.Current.CancellationToken);

        Assert.Null(read);
        Assert.True(stream.Position <= 1024 + RequestBodyReader.ChunkSize, $"read {stream.Position} bytes");
    }
}
