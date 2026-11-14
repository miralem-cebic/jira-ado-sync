using Azure.Storage.Queues;

namespace JiraAdoSync.Azure;

/// <summary>
/// The storage clients that must be configured in one way, so that the host and the tests cannot differ.
/// </summary>
public static class StorageClients
{
    /// <summary>
    /// Creates the client of the delivery queue with Base64 message encoding. The Functions queue trigger expects Base64
    /// by default. A message sent as plain text cannot be decoded, and ends in the poison queue after five attempts.
    /// </summary>
    /// <param name="connectionString">The storage connection string.</param>
    /// <param name="queueName">The name of the delivery queue.</param>
    /// <param name="options">Optional client options, for example a pinned service version. Its encoding is always Base64.</param>
    public static QueueClient CreateDeliveryQueue(string connectionString, string queueName, QueueClientOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        ArgumentException.ThrowIfNullOrEmpty(queueName);

        options ??= new QueueClientOptions();
        options.MessageEncoding = QueueMessageEncoding.Base64;
        return new QueueClient(connectionString, queueName, options);
    }
}
