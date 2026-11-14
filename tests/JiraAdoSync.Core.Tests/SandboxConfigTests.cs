using System.Text.Json.Nodes;
using JiraAdoSync.Core.Config;
using NJsonSchema;

namespace JiraAdoSync.Core.Tests;

/// <summary>
/// The sandbox configuration (M51 ↔ Neo) must stay valid against the schema and loadable by the loader, so the
/// repository keeps a working sandbox mapping as the project grows.
/// </summary>
public class SandboxConfigTests
{
    private static readonly string SchemaText = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "config.schema.json"));

    private static readonly string SandboxText = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "sandbox.json"));

    [Fact]
    public async Task SandboxConfigPassesSchema()
    {
        var schema = await JsonSchema.FromJsonAsync(SchemaText).ConfigureAwait(true);
        var errors = schema.Validate(SandboxText);
        Assert.True(errors.Count == 0, $"sandbox.json fails schema: {string.Join("; ", errors)}");
    }

    [Fact]
    public void SandboxConfigLoads()
    {
        var result = SyncConfigLoader.Parse(SandboxText);
        Assert.True(result.IsValid, $"sandbox.json fails loader: {string.Join("; ", result.Errors)}");
    }

    [Fact]
    public void SandboxConfigMapsM51ToNeo()
    {
        var result = SyncConfigLoader.Parse(SandboxText);
        Assert.True(result.IsValid);
        var mapping = Assert.Single(result.Config!.ProjectMappings);
        Assert.Equal("M51", mapping.JiraProject);
        Assert.Equal("Neo", mapping.AdoProject);
        Assert.Equal("Neo", mapping.AreaPath);
    }
}
