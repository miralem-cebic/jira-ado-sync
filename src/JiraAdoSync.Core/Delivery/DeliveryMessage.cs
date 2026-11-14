using JiraAdoSync.Core.Model;

namespace JiraAdoSync.Core.Delivery;

/// <summary>What the queue carries: the source and the blob name of the stored body. The payload itself stays in the store.</summary>
/// <param name="Source">The side that sent the delivery.</param>
/// <param name="BlobName">The name the body was stored under, see <see cref="DeliveryKey"/>.</param>
public sealed record DeliveryMessage(Side Source, string BlobName);
