using DisciplesRemaster.Content.Catalog;
using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Core.Movement;
using DisciplesRemaster.Core.Navigation;
using DisciplesRemaster.Core.Sessions;
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
            Sample("scenarios", "minimal-scenario.json"),
            [Sample("content", "synthetic.package.json")]);

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

        GameSessionLoadResult result = store.Load(Sample("sessions", "minimal-session.json"));

        Assert.True(result.IsSuccess);
        Assert.Equal("blue", result.Session!.Turn.ActiveParticipantId);
        Assert.Equal(1, result.Session.Turn.RoundNumber);
        Assert.Equal(2, result.Session.Actors.Count);
        Assert.Equal(4, result.Session.Actors["blue-actor"].RemainingMovement);
    }

    [Fact]
    public void TrackedProjectManifest_LoadsCompleteValidatedProject()
    {
        var scenarioValidation = new ScenarioValidationService();
        var packageValidation = new ContentPackageValidationService();
        var loader = new NativeProjectLoader(
            new NativeProjectManifestFileStore(
                new NativeProjectManifestJsonSerializer(new NativeProjectManifestValidationService())),
            new ScenarioBundleLoader(
                new ScenarioFileStore(new ScenarioJsonSerializer(scenarioValidation)),
                new ContentPackageFileStore(new ContentPackageJsonSerializer(packageValidation)),
                packageValidation,
                new ScenarioContentValidationService()),
            new GameSessionFileStore(new GameSessionJsonSerializer()));

        var sceneLoader = new NativeProjectSceneLoader(loader, scenarioValidation);

        NativeProjectSceneLoadResult result = sceneLoader.Load(Sample("minimal.project.json"));

        Assert.True(result.IsSuccess);
        Assert.Equal("synthetic-project", result.Project!.ProjectId);
        Assert.Equal("synthetic-minimal", result.Project.Scene.ScenarioId);
        Assert.Equal(["synthetic"], result.Project.ContentPackageIds);
        Assert.NotNull(result.Project.Session);
        Assert.Equal(result.Project.Scene.Size, result.Project.Session.MapSize);
    }

    [Fact]
    public void TrackedActionLog_ReplaysDeterministicallyThroughProductionStores()
    {
        var sessionStore = new GameSessionFileStore(new GameSessionJsonSerializer());
        var actionStore = new GameSessionActionLogFileStore(new GameSessionActionLogJsonSerializer());
        GameSessionLoadResult session = sessionStore.Load(Sample("sessions", "minimal-session.json"));
        GameSessionActionLogLoadResult actions = actionStore.Load(Sample("sessions", "minimal-actions.json"));
        var processor = new GameSessionActionProcessor(
            new GameSessionService(new MovementPlanner(new GridPathfinder())));

        GameSessionActionBatchResult result = processor.Apply(
            session.Session!,
            actions.Log!.Actions,
            OrthogonalGridTopology.Instance,
            _ => true);

        Assert.True(session.IsSuccess);
        Assert.True(actions.IsSuccess);
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.AppliedActions);
        Assert.Equal("red", result.Session!.Turn.ActiveParticipantId);
        Assert.Equal(new GridPosition(2, 1), result.Session.Actors["blue-actor"].Position);
        Assert.Equal(new GridPosition(5, 4), result.Session.Actors["red-actor"].Position);
    }

    [Fact]
    public void TrackedRuntimeSamples_AreProjectOwnedJsonOnly()
    {
        string[] files = Directory.GetFiles(SamplesDirectory, "*", SearchOption.AllDirectories);

        Assert.Equal(5, files.Length);
        Assert.All(files, path => Assert.EndsWith(".json", path, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(files, path =>
            Path.GetExtension(path) is ".sg" or ".sav" or ".exe" or ".dll");
    }

    private static string Sample(params string[] pathSegments) =>
        Path.Combine([SamplesDirectory, .. pathSegments]);
}
