using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Godot;
using DisciplesRemaster.Persistence.Scenarios;

namespace DisciplesRemaster.Game.Tests;

public sealed class ScenarioSceneLoaderTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"d2r-game-{Guid.NewGuid():N}");
    private readonly ScenarioFileStore store;
    private readonly ScenarioSceneLoader loader;

    public ScenarioSceneLoaderTests()
    {
        Directory.CreateDirectory(directory);
        var validation = new ScenarioValidationService();
        store = new ScenarioFileStore(new ScenarioJsonSerializer(validation));
        loader = new ScenarioSceneLoader(store, validation);
    }

    [Fact]
    public void Load_ValidScenario_ProjectsDeterministicSceneData()
    {
        string path = Path.Combine(directory, "scene.json");
        ScenarioDefinition scenario = CreateScenario();
        Assert.True(store.Save(path, scenario).IsSuccess);

        ScenarioSceneLoadResult result = loader.Load(path);

        Assert.True(result.IsSuccess);
        Assert.Equal("synthetic", result.Scene!.ScenarioId);
        Assert.Equal(new GridSize(8, 6), result.Scene.Size);
        Assert.Equal("synthetic:plain", result.Scene.DefaultTerrain);
        Assert.Equal(["synthetic:forest", "synthetic:water"], result.Scene.TerrainOverrides.Select(item => item.ContentReference));
        Assert.Equal(["a-object", "z-object"], result.Scene.Objects.Select(item => item.Id));
    }

    [Fact]
    public void Load_MissingScenario_ReturnsStructuredFailure()
    {
        ScenarioSceneLoadResult result = loader.Load(Path.Combine(directory, "missing.json"));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Scene);
        Assert.Equal(ScenarioPersistenceErrorCode.FileNotFound, result.ErrorCode);
    }

    [Fact]
    public void Load_InvalidScenario_ReturnsStructuredFailure()
    {
        string path = Path.Combine(directory, "invalid.json");
        File.WriteAllText(path, "not-json");

        ScenarioSceneLoadResult result = loader.Load(path);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScenarioPersistenceErrorCode.InvalidJson, result.ErrorCode);
    }

    public void Dispose()
    {
        Directory.Delete(directory, true);
        GC.SuppressFinalize(this);
    }

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
                [
                    new TerrainPlacement(new GridPosition(4, 2), "synthetic:water"),
                    new TerrainPlacement(new GridPosition(1, 1), "synthetic:forest"),
                ])
            {
                Objects =
                [
                    new ScenarioObjectPlacement("z-object", "synthetic:z", new GridPosition(5, 4)),
                    new ScenarioObjectPlacement("a-object", "synthetic:a", new GridPosition(2, 3)),
                ],
            });
}
