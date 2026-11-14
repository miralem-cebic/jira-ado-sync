using JiraAdoSync.Functions;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(SyncHostRegistration.ConfigureServices)
    .Build();

host.Run();
