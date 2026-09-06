using DisciplesRemaster.Content.Catalog;
using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Core.Geometry;

namespace DisciplesRemaster.Content.Tests;

public sealed class ContentCatalogTests
{
    private readonly ContentPackageValidationService validation = new();

    [Theory]
    [InlineData("base:plain", true)]
    [InlineData("my-pack:terrain/forest", true)]
    [InlineData("Base:plain", false)]
    [InlineData("plain", false)]
    [InlineData("base:", false)]
    [InlineData("base:bad value", false)]
    [InlineData("base:one:two", false)]
    public void ContentReference_TryParse_ValidatesSyntax(string value, bool expected)
    {
        Assert.Equal(expected, ContentReference.TryParse(value, out _));
    }

    [Fact]
    public void Validate_ValidPackage_ReturnsNoIssues()
    {
        ContentPackageValidationResult result = validation.Validate(CreatePackage());

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Validate_InvalidPackage_ReturnsStableOrderedIssues()
    {
        ContentPackageDefinition package = CreatePackage() with
        {
            Id = "Bad Package",
            DisplayName = string.Empty,
            Terrains =
            [
                new TerrainContentDefinition("same", "First"),
                new TerrainContentDefinition("same", "Second"),
            ],
        };

        ContentPackageValidationResult first = validation.Validate(package);
        ContentPackageValidationResult second = validation.Validate(package);

        Assert.False(first.IsValid);
        Assert.Contains(first.Issues, issue => issue.Code == ContentPackageValidationCode.PackageIdInvalid);
        Assert.Contains(first.Issues, issue => issue.Code == ContentPackageValidationCode.PackageDisplayNameMissing);
        Assert.Contains(first.Issues, issue => issue.Code == ContentPackageValidationCode.DuplicateTerrainId);
        Assert.Equal(first.Issues, second.Issues);
    }

    [Fact]
    public void Build_ValidPackages_IndexesTypedReferences()
    {
        ContentCatalogBuildResult result = ContentCatalog.Build([CreatePackage()], validation);

        Assert.True(result.IsSuccess);
        Assert.True(result.Catalog!.ContainsTerrain("synthetic:plain"));
        Assert.True(result.Catalog.ContainsObjectArchetype("synthetic:landmark"));
        Assert.False(result.Catalog.ContainsTerrain("synthetic:landmark"));
        Assert.False(result.Catalog.ContainsObjectArchetype("synthetic:plain"));
    }

    [Fact]
    public void Build_DuplicatePackageIds_ReturnsStructuredFailure()
    {
        ContentCatalogBuildResult result = ContentCatalog.Build([CreatePackage(), CreatePackage()], validation);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Catalog);
        Assert.Contains(result.Issues, issue => issue.Code == ContentCatalogBuildErrorCode.DuplicatePackageId);
    }

    [Fact]
    public void Build_InvalidPackage_DoesNotCreatePartialCatalog()
    {
        ContentPackageDefinition invalid = CreatePackage() with { Id = string.Empty };

        ContentCatalogBuildResult result = ContentCatalog.Build([CreatePackage(), invalid], validation);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Catalog);
        Assert.Contains(result.Issues, issue => issue.Code == ContentCatalogBuildErrorCode.InvalidPackage);
    }

    [Fact]
    public void ScenarioContentValidation_WithKnownReferences_IsValid()
    {
        ContentCatalog catalog = ContentCatalog.Build([CreatePackage()], validation).Catalog!;
        ScenarioDefinition scenario = CreateScenario();

        ScenarioContentValidationResult result = new ScenarioContentValidationService()
            .ValidateReferences(scenario, catalog);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ScenarioContentValidation_WithUnknownReferences_ReturnsTypedIssues()
    {
        ContentCatalog catalog = ContentCatalog.Build([CreatePackage()], validation).Catalog!;
        ScenarioDefinition scenario = CreateScenario() with
        {
            Map = CreateScenario().Map with
            {
                DefaultTerrain = "missing:plain",
                Terrain = [new TerrainPlacement(new GridPosition(1, 1), "missing:forest")],
                Objects = [new ScenarioObjectPlacement("object", "missing:object", new GridPosition(2, 2))],
            },
        };

        ScenarioContentValidationResult result = new ScenarioContentValidationService()
            .ValidateReferences(scenario, catalog);

        Assert.Equal(3, result.Issues.Count);
        Assert.Contains(result.Issues, issue => issue.Code == ScenarioContentValidationCode.UnknownDefaultTerrain);
        Assert.Contains(result.Issues, issue => issue.Code == ScenarioContentValidationCode.UnknownTerrain);
        Assert.Contains(result.Issues, issue => issue.Code == ScenarioContentValidationCode.UnknownObjectArchetype);
    }

    private static ContentPackageDefinition CreatePackage() =>
        new(
            ContentPackageFormatV1.Version,
            "synthetic",
            "Synthetic package",
            [
                new TerrainContentDefinition("plain", "Plain"),
                new TerrainContentDefinition("forest", "Forest"),
            ],
            [
                new ObjectArchetypeDefinition("landmark", "Landmark"),
            ]);

    private static ScenarioDefinition CreateScenario() =>
        new(
            ScenarioFormatV1.Version,
            "synthetic",
            "Synthetic",
            null,
            new ScenarioMapDefinition(
                8,
                6,
                "synthetic:plain",
                [new TerrainPlacement(new GridPosition(1, 1), "synthetic:forest")])
            {
                Objects = [new ScenarioObjectPlacement("object", "synthetic:landmark", new GridPosition(2, 2))],
            });
}
