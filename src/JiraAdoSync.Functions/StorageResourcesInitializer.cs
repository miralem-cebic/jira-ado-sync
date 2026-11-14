using JiraAdoSync.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace JiraAdoSync.Functions;

/// <summary>Creates the container, the queue and the table at startup if they are missing. Idempotent.</summary>
public sealed class StorageResourcesInitializer(IConfiguration configuration) : IHostedService
{
    private readonly string _storage = configuration[SyncNames.StorageConnectionSetting]
        ?? throw new InvalidOperationException($"The app setting {SyncNames.StorageConnectionSetting} is not set.");

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => StorageResources.EnsureCreatedAsync(_storage, cancellationToken);

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
