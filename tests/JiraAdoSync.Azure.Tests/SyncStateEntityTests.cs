using JiraAdoSync.Core.Model;

namespace JiraAdoSync.Azure.Tests;

public class SyncStateEntityTests
{
    private static readonly SyncState State = new(
        "PROJ-1",
        "42",
        "ABCDEF0123",
        new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero),
        Side.Jira);

    [Fact]
    public void StateSurvivesARoundTripThroughTheEntity()
    {
        var entity = SyncStateEntity.ToEntity("proj", State);

        Assert.Equal(State, SyncStateEntity.ToState(entity));
    }

    [Fact]
    public void EntityIsKeyedByMappingAndJiraKey()
    {
        var entity = SyncStateEntity.ToEntity("proj", State);

        Assert.Equal("proj", entity.PartitionKey);
        Assert.Equal("PROJ-1", entity.RowKey);
        Assert.Equal("42", entity["AdoWorkItemId"]);
    }

    [Fact]
    public void LastWriterIsStoredByItsName()
    {
        var entity = SyncStateEntity.ToEntity("proj", State);

        Assert.Equal("jira", entity["LastWriter"]);
    }
}
