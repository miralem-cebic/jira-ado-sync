using JiraAdoSync.Core.Delivery;
using JiraAdoSync.Core.Sync;

namespace JiraAdoSync.Azure.Tests;

public class DeliveryRetryTests
{
    [Fact]
    public void ALostSaveRaceIsRetried()
    {
        Assert.True(DeliveryRetry.IsRequired(new DeliveryOutcome.RetryLater("PROJ-1")));
    }

    [Fact]
    public void AProcessedOrDroppedDeliveryIsNotRetried()
    {
        Assert.False(DeliveryRetry.IsRequired(new DeliveryOutcome.Processed(new SyncOutcome.Written(), "PROJ-1")));
        Assert.False(DeliveryRetry.IsRequired(new DeliveryOutcome.Dropped(DropReason.NotJson)));
    }
}
