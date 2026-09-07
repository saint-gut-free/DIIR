using DisciplesRemaster.Content.Catalog;
using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Persistence.Content;
using DisciplesRemaster.Persistence.Projects;
using DisciplesRemaster.Persistence.Scenarios;

namespace DisciplesRemaster.Persistence.Tests;

public sealed class ScenarioBundleLoaderTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"d2r-bundle-{Guid.NewGuid():N}");
    private readonly ScenarioFileStore scenarioStore;
    private readonly ContentPackageFileStore contentStore;
    private readonly ScenarioBundleLoader loader;

    public ScenarioBundleLoaderTests()
    {
        Directory.CreateDirectory(directory);
        var scenarioValidation = new ScenarioValidationService();
        var packageValidation = new ContentPackageValidationService();
        scenarioStore = new ScenarioFileStore(new ScenarioJsonSerializer(scenarioValidation));
        contentStore = new ContentPackageFileStore(new ContentPackageJsonSerializer(packageValidation));
        loader = new ScenarioBundleLoader(
            scenarioStore,
            contentStore,
            packageValidation,
            new ScenarioContentValidationService());
    }

    [Fact]
    public void Load_ValidScenarioAndPackages_ReturnsResolvedBundle()
    {
        string scenarioPath = SaveScenario(CreateScenario());
        string secondaryPath = SavePackage(CreatePackage("secondary"));
        string syntheticPath = SavePackage(CreatePackage("synthetic"));

        ScenarioBundleLoadResult result = loader.Load(scenarioPath, [secondaryPath, syntheticPath]);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Bundle);
        Assert.Equal(["secondary", "synthetic"], result.Bundle.ContentPackageIds);
        Assert.True(result.Bundle.Content.ContainsTerrain("synthetic:plain"));
        Assert.True(result.Bundle.Content.ContainsObjectArchetype("synthetic:leader"));
    }

    [Fact]
    public void Load_MissingScenario_ReturnsStructuredIssue()
    {
        ScenarioBundleLoadResult result = loader.Load(Path.Combine(directory, "missing.json"), []);

        ScenarioBundleLoadIssue issue = Assert.Single(result.Issues);
        Assert.False(result.IsSuccess);
        Assert.Equal(ScenarioBundleLoadIssueCode.ScenarioLoadFailed, issue.Code);
        Assert.Equal(nameof(ScenarioPersistenceErrorCode.FileNotFound), issue.DetailCode);
        Assert.DoesNotContain(directory, issue.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_MissingContentPackage_ReturnsStructuredIssueWithoutPath()
    {
        string scenarioPath = SaveScenario(CreateScenario());
        string missingPath = Path.Combine(directory, "private", "missing.json");

        ScenarioBundleLoadResult result = loader.Load(scenarioPath, [missingPath]);

        ScenarioBundleLoadIssue issue = Assert.Single(result.Issues);
        Assert.Equal(ScenarioBundleLoadIssueCode.ContentPackageLoadFailed, issue.Code);
        Assert.Equal("content[0]", issue.InputLabel);
        Assert.Equal(nameof(ContentPackagePersistenceErrorCode.FileNotFound), issue.DetailCode);
        Assert.DoesNotContain(directory, issue.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_DuplicatePackageIds_ReturnsCatalogIssue()
    {
        string scenarioPath = SaveScenario(CreateScenario());
        string first = SavePackage(CreatePackage("synthetic"), "first.json");
        string second = SavePackage(CreatePackage("synthetic"), "second.json");

        ScenarioBundleLoadResult result = loader.Load(scenarioPath, [first, second]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue =>
            issue.Code == ScenarioBundleLoadIssueCode.ContentCatalogInvalid &&
            issue.DetailCode == nameof(ContentCatalogBuildErrorCode.DuplicatePackageId));
    }

    [Fact]
    public void Load_UnknownScenarioReferences_ReturnsAllSortedIssues()
    {
        ScenarioDefinition scenario = CreateScenario() with
        {
            Map = CreateScenario().Map with
            {
                DefaultTerrain = "missing:plain",
                Terrain = [new TerrainPlacement(new GridPosition(1, 1), "missing:water")],
                Objects = [new ScenarioObjectPlacement("leader-1", "missing:leader", new GridPosition(2, 2))],
            },
        };
        string scenarioPath = SaveScenario(scenario);
        string packagePath = SavePackage(CreatePackage("synthetic"));

        ScenarioBundleLoadResult result = loader.Load(scenarioPath, [packagePath]);

        Assert.False(result.IsSuccess);
        Assert.Equal(3, result.Issues.Count);
        Assert.All(result.Issues, issue => Assert.Equal(ScenarioBundleLoadIssueCode.ScenarioContentInvalid, issue.Code));
        Assert.Equal(
            result.Issues.OrderBy(issue => issue.PropertyPath, StringComparer.Ordinal),
            result.Issues);
    }

    [Fact]
    public void Load_InvalidContentDocument_ReportsStablePersistenceCode()
    {
        string scenarioPath = SaveScenario(CreateScenario());
        string packagePath = Path.Combine(directory, "invalid-content.json");
        File.WriteAllText(packagePath, "not-json");

        ScenarioBundleLoadResult result = loader.Load(scenarioPath, [packagePath]);

        ScenarioBundleLoadIssue issue = Assert.Single(result.Issues);
        Assert.Equal(nameof(ContentPackagePersistenceErrorCode.InvalidJson), issue.DetailCode);
    }

    [Fact]
    public void Load_EmptyCatalog_DoesNotInventFallbackContent()
    {
        string scenarioPath = SaveScenario(CreateScenario());

        ScenarioBundleLoadResult result = loader.Load(scenarioPath, []);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.DetailCode == nameof(ScenarioContentValidationCode.UnknownDefaultTerrain));
    }

    public void Dispose()
    {
        Directory.Delete(directory, true);
        GC.SuppressFinalize(this);
    }

    private string SaveScenario(ScenarioDefinition scenario)
    {
        string path = Path.Combine(directory, "scenario.json");
        Assert.True(scenarioStore.Save(path, scenario).IsSuccess);
        return path;
    }

    private string SavePackage(ContentPackageDefinition package, string? filename = null)
    {
        string path = Path.Combine(directory, filename ?? $"{package.Id}.content.json");
        Assert.True(contentStore.Save(path, package).IsSuccess);
        return path;
    }

    private static ScenarioDefinition CreateScenario() =>
        new(
            ScenarioFormatV1.Version,
            "synthetic",
            "Synthetic scenario",
            null,
            new ScenarioMapDefinition(8, 6, "synthetic:plain", [])
            {
                Objects =
                [
                    new ScenarioObjectPlacement("leader-1", "synthetic:leader", new GridPosition(2, 2)),
                ],
            });

    private static ContentPackageDefinition CreatePackage(string id) =>
        new(
            ContentPackageFormatV1.Version,
            id,
            $"{id} package",
            [new TerrainContentDefinition("plain", "Plain")],
            [new ObjectArchetypeDefinition("leader", "Leader")]);
}
