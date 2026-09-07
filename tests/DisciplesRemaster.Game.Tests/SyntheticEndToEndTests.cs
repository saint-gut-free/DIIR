using DisciplesRemaster.Content.Catalog;
using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Godot;
using DisciplesRemaster.Persistence.Content;
using DisciplesRemaster.Persistence.Projects;
using DisciplesRemaster.Persistence.Scenarios;
using DisciplesRemaster.Persistence.Sessions;

namespace DisciplesRemaster.Game.Tests;

public sealed class SyntheticEndToEndTests
{
    private static readonly string SamplesDirectory = Path.Combine(AppContext.BaseDirectory, "Samples");

    [Fact]
    public void TrackedScenarioAndContentSamples_LoadAsValidatedScene()
    {
        var scenarioValidation = new ScenarioValidationService();
        var packageValidation = new ContentPackageValidationService();
        var bundleLoader = new ScenarioBundleLoader(
            new ScenarioFileStore(new ScenarioJsonSerializer(scenarioValidation)),
            new ContentPackageFileStore(new ContentPackageJsonSerializer(packageValidation)),
            packageValidation,
            new ScenarioContentValidationService());
        var sceneLoader = new ValidatedScenarioSceneLoader(bundleLoader, scenarioValidation);

        ValidatedScenarioSceneLoadResult result = sceneLoader.Load(
            Sample("minimal-scenario.json"),
            [Sample("synthetic.package.json")]);

        Assert.True(result.IsSuccess);
        Assert.Equal("synthetic-minimal", result.Scene!.ScenarioId);
        Assert.Equal(2, result.Scene.TerrainOverrides.Count);
        Assert.Equal("landmark-1", Assert.Single(result.Scene.Objects).Id);
        Assert.Equal(["synthetic"], result.ContentPackageIds);
    }

    [Fact]
    public void TrackedSessionSample_LoadsThroughProductionStore()
    {
        var store = new GameSessionFileStore(new GameSessionJsonSerializer());

        GameSessionLoadResult result = store.Load(Sample("minimal-session.json"));

        Assert.True(result.IsSuccess);
        Assert.Equal("blue", result.Session!.Turn.ActiveParticipantId);
        Assert.Equal(1, result.Session.Turn.RoundNumber);
        Assert.Equal(2, result.Session.Actors.Count);
        Assert.Equal(4, result.Session.Actors["blue-actor"].RemainingMovement);
    }

    [Fact]
    public void TrackedRuntimeSamples_AreProjectOwnedJsonOnly()
    {
        string[] files = Directory.GetFiles(SamplesDirectory, "*", SearchOption.TopDirectoryOnly);

        Assert.Equal(3, files.Length);
        Assert.All(files, path => Assert.EndsWith(".json", path, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(files, path =>
            Path.GetExtension(path) is ".sg" or ".sav" or ".exe" or ".dll");
    }

    private static string Sample(string filename) =>
        Path.Combine(SamplesDirectory, filename);
}
