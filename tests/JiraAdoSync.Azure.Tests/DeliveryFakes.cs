using JiraAdoSync.Core.Delivery;
using JiraAdoSync.Core.Ports;

namespace JiraAdoSync.Azure.Tests;

internal sealed class RecordingRawStore : IRawDeliveryStore
{
    private readonly Dictionary<string, byte[]> _blobs = new(StringComparer.Ordinal);

    public bool FailStores { get; set; }

    public int Count => _blobs.Count;

    public Task<bool> TryStoreAsync(string blobName, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        if (FailStores)
        {
            return Task.FromResult(false);
        }

        _blobs[blobName] = body.ToArray();
        return Task.FromResult(true);
    }

    public Task<byte[]?> LoadAsync(string blobName, CancellationToken cancellationToken) =>
        Task.FromResult(_blobs.TryGetValue(blobName, out var body) ? body : null);
}

internal sealed class RecordingDispatcher : IDeliveryDispatcher
{
    public List<DeliveryMessage> Messages { get; } = [];

    public Task<bool> TryEnqueueAsync(DeliveryMessage message, CancellationToken cancellationToken)
    {
        Messages.Add(message);
        return Task.FromResult(true);
    }
}

/// <summary>A request body whose read fails, as when a client disconnects while sending it.</summary>
internal sealed class UnreadableStream : Stream
{
    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => 0;
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new IOException("connection reset");

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        ValueTask.FromException<int>(new IOException("connection reset"));

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
