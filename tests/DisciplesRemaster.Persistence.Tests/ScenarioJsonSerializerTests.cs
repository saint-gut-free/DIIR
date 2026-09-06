using System.Security.Cryptography;
using System.Text;
using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Persistence.Scenarios;

namespace DisciplesRemaster.Persistence.Tests;

public sealed class ScenarioJsonSerializerTests
{
    private readonly ScenarioJsonSerializer serializer = new(new ScenarioValidationService());

    [Fact]
    public void Serialize_ValidScenario_ProducesCamelCaseUtf8Json()
    {
        ScenarioSerializationResult result = serializer.Serialize(CreateScenario());

        Assert.True(result.IsSuccess);
        string json = Encoding.UTF8.GetString(result.Data!);
        Assert.Contains("\"formatVersion\": 1", json, StringComparison.Ordinal);
        Assert.Contains("\"defaultTerrain\": \"synthetic:plain\"", json, StringComparison.Ordinal);
        Assert.EndsWith("\n", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_InvalidScenario_ReturnsValidationFailure()
    {
        ScenarioDefinition invalid = CreateScenario() with { Id = string.Empty };

        ScenarioSerializationResult result = serializer.Serialize(invalid);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScenarioPersistenceErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == ScenarioValidationCode.ScenarioIdMissing);
    }

    [Fact]
    public void Serialize_DifferentTerrainOrder_ProducesIdenticalBytes()
    {
        TerrainPlacement first = new(new GridPosition(2, 1), "synthetic:water");
        TerrainPlacement second = new(new GridPosition(1, 0), "synthetic:forest");
        ScenarioDefinition left = CreateScenario([first, second]);
        ScenarioDefinition right = CreateScenario([second, first]);

        byte[] leftData = serializer.Serialize(left).Data!;
        byte[] rightData = serializer.Serialize(right).Data!;

        Assert.Equal(leftData, rightData);
        Assert.Equal(
            Convert.ToHexStringLower(SHA256.HashData(leftData)),
            Convert.ToHexStringLower(SHA256.HashData(rightData)));
    }

    [Fact]
    public void Deserialize_SerializedScenario_RoundTrips()
    {
        ScenarioDefinition expected = CreateScenario(
        [
            new TerrainPlacement(new GridPosition(3, 2), "synthetic:forest"),
        ]);
        byte[] data = serializer.Serialize(expected).Data!;

        ScenarioDeserializationResult result = serializer.Deserialize(data);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected.FormatVersion, result.Scenario!.FormatVersion);
        Assert.Equal(expected.Id, result.Scenario.Id);
        Assert.Equal(expected.Title, result.Scenario.Title);
        Assert.Equal(expected.Description, result.Scenario.Description);
        Assert.Equal(expected.Map.Width, result.Scenario.Map.Width);
        Assert.Equal(expected.Map.Height, result.Scenario.Map.Height);
        Assert.Equal(expected.Map.DefaultTerrain, result.Scenario.Map.DefaultTerrain);
        Assert.Equal(expected.Map.Terrain.ToArray(), result.Scenario.Map.Terrain.ToArray());
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{broken")]
    [InlineData("{\"formatVersion\":1,\"unknown\":true}")]
    public void Deserialize_InvalidJson_ReturnsStructuredFailure(string json)
    {
        ScenarioDeserializationResult result = serializer.Deserialize(Encoding.UTF8.GetBytes(json));

        Assert.False(result.IsSuccess);
        Assert.Equal(ScenarioPersistenceErrorCode.InvalidJson, result.ErrorCode);
    }

    [Fact]
    public void Deserialize_SemanticallyInvalidDocument_ReturnsValidationIssues()
    {
        const string json = """
            {
              "formatVersion": 1,
              "id": "",
              "title": "Synthetic",
              "description": null,
              "map": {
                "width": 4,
                "height": 4,
                "defaultTerrain": "synthetic:plain",
                "terrain": []
              }
            }
            """;

        ScenarioDeserializationResult result = serializer.Deserialize(Encoding.UTF8.GetBytes(json));

        Assert.False(result.IsSuccess);
        Assert.Equal(ScenarioPersistenceErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == ScenarioValidationCode.ScenarioIdMissing);
    }

    [Fact]
    public void Deserialize_NullTerrainEntry_ReturnsInvalidJsonWithoutThrowing()
    {
        const string json = """
            {
              "formatVersion": 1,
              "id": "synthetic-scenario",
              "title": "Synthetic",
              "description": null,
              "map": {
                "width": 4,
                "height": 4,
                "defaultTerrain": "synthetic:plain",
                "terrain": [null]
              }
            }
            """;

        ScenarioDeserializationResult result = serializer.Deserialize(Encoding.UTF8.GetBytes(json));

        Assert.False(result.IsSuccess);
        Assert.Equal(ScenarioPersistenceErrorCode.InvalidJson, result.ErrorCode);
    }

    private static ScenarioDefinition CreateScenario(IReadOnlyList<TerrainPlacement>? terrain = null) =>
        new(
            ScenarioFormatV1.Version,
            "synthetic-scenario",
            "Synthetic scenario",
            "Project-owned test data.",
            new ScenarioMapDefinition(8, 6, "synthetic:plain", terrain ?? []));
}
