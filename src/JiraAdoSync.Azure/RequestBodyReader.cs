namespace JiraAdoSync.Azure;

/// <summary>Reads a request body with an upper bound, so that a large body never fills memory (ADR 0009).</summary>
public static class RequestBodyReader
{
    /// <summary>The number of bytes requested per read.</summary>
    public const int ChunkSize = 81920;

    /// <summary>
    /// Returns the whole body, or null when it has more than <paramref name="maxBytes"/> bytes. The read stops at the first
    /// chunk that goes over the limit, so at most one chunk beyond the limit is read.
    /// </summary>
    /// <param name="body">The body stream.</param>
    /// <param name="maxBytes">The largest body that is accepted.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public static async Task<byte[]?> ReadAsync(Stream body, int maxBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentOutOfRangeException.ThrowIfNegative(maxBytes);

        using var buffer = new MemoryStream();
        var chunk = new byte[ChunkSize];
        while (true)
        {
            var read = await body.ReadAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }
}
