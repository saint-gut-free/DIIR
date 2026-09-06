using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Persistence.Scenarios;

namespace DisciplesRemaster.Persistence.Tests;

public sealed class ScenarioFileStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"d2r-scenario-{Guid.NewGuid():N}");
    private readonly ScenarioFileStore store;

    public ScenarioFileStoreTests()
    {
        Directory.CreateDirectory(directory);
        store = new ScenarioFileStore(new ScenarioJsonSerializer(new ScenarioValidationService()));
    }

    [Fact]
    public void SaveAndLoad_ValidScenario_RoundTrips()
    {
        string path = Path.Combine(directory, "scenario.json");
        ScenarioDefinition expected = CreateScenario();

        ScenarioSaveResult save = store.Save(path, expected);
        ScenarioLoadResult load = store.Load(path);

        Assert.True(save.IsSuccess);
        Assert.True(load.IsSuccess);
        Assert.Equal(expected.FormatVersion, load.Scenario!.FormatVersion);
        Assert.Equal(expected.Id, load.Scenario.Id);
        Assert.Equal(expected.Title, load.Scenario.Title);
        Assert.Equal(expected.Map.Width, load.Scenario.Map.Width);
        Assert.Equal(expected.Map.Height, load.Scenario.Map.Height);
    }

    [Fact]
    public void Save_OverwritesExistingFileAtomically()
    {
        string path = Path.Combine(directory, "scenario.json");
        Assert.True(store.Save(path, CreateScenario() with { Title = "First" }).IsSuccess);

        ScenarioSaveResult save = store.Save(path, CreateScenario() with { Title = "Second" });
        ScenarioLoadResult load = store.Load(path);

        Assert.True(save.IsSuccess);
        Assert.Equal("Second", load.Scenario!.Title);
        Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
    }

    [Fact]
    public void Save_InvalidScenario_DoesNotCreateFile()
    {
        string path = Path.Combine(directory, "scenario.json");

        ScenarioSaveResult result = store.Save(path, CreateScenario() with { Id = string.Empty });

        Assert.False(result.IsSuccess);
        Assert.Equal(ScenarioPersistenceErrorCode.ValidationFailed, result.ErrorCode);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Save_MissingOutputDirectory_ReturnsInvalidPath()
    {
        string path = Path.Combine(directory, "missing", "scenario.json");

        ScenarioSaveResult result = store.Save(path, CreateScenario());

        Assert.False(result.IsSuccess);
        Assert.Equal(ScenarioPersistenceErrorCode.InvalidPath, result.ErrorCode);
        Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
    }

    [Fact]
    public void Load_MissingFile_ReturnsFileNotFound()
    {
        ScenarioLoadResult result = store.Load(Path.Combine(directory, "missing.json"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ScenarioPersistenceErrorCode.FileNotFound, result.ErrorCode);
    }

    [Fact]
    public void Load_InvalidJson_ReturnsInvalidJson()
    {
        string path = Path.Combine(directory, "invalid.json");
        File.WriteAllText(path, "not-json");

        ScenarioLoadResult result = store.Load(path);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScenarioPersistenceErrorCode.InvalidJson, result.ErrorCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Operations_InvalidPath_ReturnStructuredFailure(string path)
    {
        Assert.Equal(ScenarioPersistenceErrorCode.InvalidPath, store.Load(path).ErrorCode);
        Assert.Equal(ScenarioPersistenceErrorCode.InvalidPath, store.Save(path, CreateScenario()).ErrorCode);
    }

    public void Dispose()
    {
        Directory.Delete(directory, true);
        GC.SuppressFinalize(this);
    }

    private static ScenarioDefinition CreateScenario() =>
        new(
            ScenarioFormatV1.Version,
            "synthetic-scenario",
            "Synthetic scenario",
            null,
            new ScenarioMapDefinition(8, 6, "synthetic:plain", []));
}
