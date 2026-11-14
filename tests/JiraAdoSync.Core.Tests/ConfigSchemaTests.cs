using System.Text.Json.Nodes;
using JiraAdoSync.Core.Config;
using NJsonSchema;

namespace JiraAdoSync.Core.Tests;

/// <summary>
/// The JSON Schema (for editors) and the loader (authoritative) must agree on every case below. Rules that a schema
/// cannot express, such as unique Jira project keys and the one-to-one status map, are tested in SyncConfigLoaderTests.
/// </summary>
/// <remarks>
/// NJsonSchema reads the schema as draft 4, whatever its <c>$schema</c> says. It ignores <c>const</c> and <c>if</c>, so
/// the schema uses only keywords that draft 4 and 2020-12 share. A rule with <c>const</c> would pass silently.
/// </remarks>
public class ConfigSchemaTests
{
    private static readonly string SchemaText = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "config.schema.json"));

    private static readonly string ExampleText = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "config.example.json"));

    public static IEnumerable<object[]> Cases()
    {
        yield return Case("the shipped example", Variant(_ => { }), valid: true);
        yield return Case("unknown root property", Variant(n => n["conflictPolicyy"] = JsonValue.Create("lastWriteWins")), valid: false);
        yield return Case("http site URL", Variant(n => Obj(n, "jira")["siteUrl"] = JsonValue.Create("http://your-site.atlassian.net")), valid: false);
        yield return Case("https without a host", Variant(n => Obj(n, "jira")["siteUrl"] = JsonValue.Create("https://")), valid: false);
        yield return Case("unknown Jira auth mode", Variant(n => Obj(n, "jira")["authMode"] = JsonValue.Create("oauth")), valid: false);
        yield return Case("PAT without patSetting", Variant(n => Obj(n, "azureDevOps")["authMode"] = JsonValue.Create("pat")), valid: false);
        yield return Case("PAT with patSetting", Variant(n =>
        {
            Obj(n, "azureDevOps")["authMode"] = JsonValue.Create("pat");
            Obj(n, "azureDevOps")["patSetting"] = JsonValue.Create("ADO_PAT");
        }), valid: true);
        yield return Case("whitespace-only organization", Variant(n => Obj(n, "azureDevOps")["organization"] = JsonValue.Create("   ")), valid: false);
        yield return Case("empty ADO state in the status map", Variant(n => Mapping(n)["statusMap"]!.AsObject()["Done"] = JsonValue.Create("")), valid: false);
        yield return Case("whitespace-only area path", Variant(n => Mapping(n)["areaPath"] = JsonValue.Create("   ")), valid: false);
        yield return Case("feature flag as text", Variant(n => Obj(n, "features")["title"] = JsonValue.Create("yes")), valid: false);
        yield return Case("features omitted", Variant(n => n.Remove("features")), valid: true);
        yield return Case("no project mapping", Variant(n => n["projectMappings"] = new JsonArray()), valid: false);
        yield return Case("conflict policy missing", Variant(n => n.Remove("conflictPolicy")), valid: false);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task SchemaAndLoaderAgree(string name, string json, bool expectedValid)
    {
        var schemaValid = await SchemaAcceptsAsync(json).ConfigureAwait(true);
        var loaderValid = SyncConfigLoader.Parse(json).IsValid;

        Assert.True(schemaValid == expectedValid, $"{name}: the schema says valid={schemaValid}, expected {expectedValid}.");
        Assert.True(loaderValid == expectedValid, $"{name}: the loader says valid={loaderValid}, expected {expectedValid}.");
    }

    private static async Task<bool> SchemaAcceptsAsync(string json)
    {
        var schema = await JsonSchema.FromJsonAsync(SchemaText).ConfigureAwait(false);
        return schema.Validate(json).Count == 0;
    }

    private static object[] Case(string name, string json, bool valid) => [name, json, valid];

    /// <summary>The shipped example, changed by <paramref name="change"/>. The example is the base of every case.</summary>
    private static string Variant(Action<JsonObject> change)
    {
        var node = (JsonObject)JsonNode.Parse(ExampleText)!;
        change(node);
        return node.ToJsonString();
    }

    private static JsonObject Obj(JsonObject parent, string name) => parent[name]!.AsObject();

    private static JsonObject Mapping(JsonObject root) => root["projectMappings"]!.AsArray()[0]!.AsObject();
}
