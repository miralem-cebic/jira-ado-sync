using JiraAdoSync.Core.Delivery;
using JiraAdoSync.Core.Ports;

namespace JiraAdoSync.Core.Tests;

internal sealed class InMemoryRawDeliveryStore : IRawDeliveryStore
{
    private readonly Dictionary<string, byte[]> _blobs = new(StringComparer.Ordinal);

    /// <summary>When true, every store call fails, as if Blob Storage were unavailable.</summary>
    public bool FailStores { get; set; }

    public int Count => _blobs.Count;

    public bool Contains(string blobName) => _blobs.ContainsKey(blobName);

    public void Seed(string blobName, byte[] body) => _blobs[blobName] = body;

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

/// <summary>Records each dispatched message and whether its body was already stored at that moment.</summary>
internal sealed class FakeDeliveryDispatcher(InMemoryRawDeliveryStore store) : IDeliveryDispatcher
{
    public List<DeliveryMessage> Messages { get; } = [];

    public bool FailEnqueue { get; set; }

    public bool StoredWhenEnqueued { get; private set; }

    public Task<bool> TryEnqueueAsync(DeliveryMessage message, CancellationToken cancellationToken)
    {
        if (FailEnqueue)
        {
            return Task.FromResult(false);
        }

        StoredWhenEnqueued = store.Contains(message.BlobName);
        Messages.Add(message);
        return Task.FromResult(true);
    }
}
