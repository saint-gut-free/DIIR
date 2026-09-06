using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Core.Geometry;

namespace DisciplesRemaster.Content.Tests;

public sealed class ScenarioValidationServiceTests
{
    private readonly ScenarioValidationService service = new();

    [Fact]
    public void Validate_WithMinimalScenario_IsValid()
    {
        ScenarioValidationResult result = service.Validate(CreateScenario());

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Validate_WithUnsupportedVersion_ReturnsStableCode()
    {
        ScenarioDefinition scenario = CreateScenario() with { FormatVersion = 2 };

        ScenarioValidationResult result = service.Validate(scenario);

        Assert.Contains(result.Issues, issue => issue.Code == ScenarioValidationCode.UnsupportedFormatVersion);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Bad ID")]
    [InlineData("UPPERCASE")]
    [InlineData("path/id")]
    public void Validate_WithInvalidId_ReturnsError(string id)
    {
        ScenarioValidationResult result = service.Validate(CreateScenario() with { Id = id });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Code is ScenarioValidationCode.ScenarioIdMissing or ScenarioValidationCode.ScenarioIdInvalid);
    }

    [Theory]
    [InlineData(0, 1, ScenarioValidationCode.MapDimensionsInvalid)]
    [InlineData(1, 0, ScenarioValidationCode.MapDimensionsInvalid)]
    [InlineData(4097, 1, ScenarioValidationCode.MapDimensionsTooLarge)]
    [InlineData(4096, 4096, ScenarioValidationCode.MapCellCountTooLarge)]
    public void Validate_WithInvalidDimensions_ReturnsExpectedCode(
        int width,
        int height,
        ScenarioValidationCode expected)
    {
        ScenarioMapDefinition map = CreateScenario().Map with { Width = width, Height = height };

        ScenarioValidationResult result = service.Validate(CreateScenario() with { Map = map });

        Assert.Contains(result.Issues, issue => issue.Code == expected);
    }

    [Fact]
    public void Validate_WithOutOfBoundsTerrain_ReturnsError()
    {
        ScenarioMapDefinition map = CreateScenario().Map with
        {
            Terrain = [new TerrainPlacement(new GridPosition(8, 0), "synthetic:forest")],
        };

        ScenarioValidationResult result = service.Validate(CreateScenario() with { Map = map });

        Assert.Contains(result.Issues, issue => issue.Code == ScenarioValidationCode.TerrainPlacementOutsideMap);
    }

    [Fact]
    public void Validate_WithDuplicateTerrainPosition_ReturnsError()
    {
        ScenarioMapDefinition map = CreateScenario().Map with
        {
            Terrain =
            [
                new TerrainPlacement(new GridPosition(1, 1), "synthetic:forest"),
                new TerrainPlacement(new GridPosition(1, 1), "synthetic:water"),
            ],
        };

        ScenarioValidationResult result = service.Validate(CreateScenario() with { Map = map });

        Assert.Contains(result.Issues, issue => issue.Code == ScenarioValidationCode.DuplicateTerrainPlacement);
    }

    [Fact]
    public void Validate_WithRedundantOverride_ReturnsWarningAndRemainsValid()
    {
        ScenarioMapDefinition map = CreateScenario().Map with
        {
            Terrain = [new TerrainPlacement(new GridPosition(1, 1), "synthetic:plain")],
        };

        ScenarioValidationResult result = service.Validate(CreateScenario() with { Map = map });

        Assert.True(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Code == ScenarioValidationCode.RedundantTerrainPlacement &&
            issue.Severity == ScenarioValidationSeverity.Warning);
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("Synthetic:plain")]
    [InlineData("synthetic plain")]
    [InlineData("C:\\local\\file")]
    public void Validate_WithInvalidContentReference_ReturnsError(string terrain)
    {
        ScenarioMapDefinition map = CreateScenario().Map with { DefaultTerrain = terrain };

        ScenarioValidationResult result = service.Validate(CreateScenario() with { Map = map });

        Assert.Contains(result.Issues, issue => issue.Code == ScenarioValidationCode.ContentReferenceInvalid);
    }

    [Fact]
    public void Validate_SortsIssuesDeterministically()
    {
        ScenarioDefinition scenario = CreateScenario() with
        {
            FormatVersion = 2,
            Id = string.Empty,
            Title = string.Empty,
            Map = CreateScenario().Map with { DefaultTerrain = string.Empty },
        };

        ScenarioValidationResult first = service.Validate(scenario);
        ScenarioValidationResult second = service.Validate(scenario);

        Assert.Equal(first.Issues, second.Issues);
        Assert.Equal(
            first.Issues.OrderBy(issue => issue.PropertyPath, StringComparer.Ordinal).ThenBy(issue => issue.Code),
            first.Issues);
    }

    private static ScenarioDefinition CreateScenario() =>
        new(
            ScenarioFormatV1.Version,
            "synthetic-scenario",
            "Synthetic scenario",
            "Project-owned test data.",
            new ScenarioMapDefinition(8, 6, "synthetic:plain", []));
}
