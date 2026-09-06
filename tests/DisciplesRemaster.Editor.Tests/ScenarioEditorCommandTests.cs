using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Editor;
using DisciplesRemaster.Persistence.Scenarios;

namespace DisciplesRemaster.Editor.Tests;

public sealed class ScenarioEditorCommandTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"d2r-editor-{Guid.NewGuid():N}");
    private readonly ScenarioFileStore store;

    public ScenarioEditorCommandTests()
    {
        Directory.CreateDirectory(directory);
        store = new ScenarioFileStore(new ScenarioJsonSerializer(new ScenarioValidationService()));
    }

    [Fact]
    public void Create_WithValidArguments_CreatesNativeScenario()
    {
        string path = Path.Combine(directory, "created.json");

        CommandResult result = Run(
            "create", path,
            "--id", "created",
            "--title", "Created scenario",
            "--width", "8",
            "--height", "6",
            "--default-terrain", "synthetic:plain");

        Assert.Equal(ScenarioEditorCommand.SuccessExitCode, result.ExitCode);
        ScenarioLoadResult loaded = store.Load(path);
        Assert.True(loaded.IsSuccess);
        Assert.Equal("created", loaded.Scenario!.Id);
        Assert.Equal(8, loaded.Scenario.Map.Width);
    }

    [Fact]
    public void Create_ExistingFileWithoutForce_DoesNotOverwrite()
    {
        string path = CreateFile("existing.json", "Original");

        CommandResult result = Run(
            "create", path,
            "--id", "replacement",
            "--title", "Replacement",
            "--width", "8",
            "--height", "6",
            "--default-terrain", "synthetic:plain");

        Assert.Equal(ScenarioEditorCommand.InputErrorExitCode, result.ExitCode);
        Assert.Equal("Original", store.Load(path).Scenario!.Title);
    }

    [Fact]
    public void Create_ExistingFileWithForce_Overwrites()
    {
        string path = CreateFile("existing.json", "Original");

        CommandResult result = Run(
            "create", path,
            "--id", "replacement",
            "--title", "Replacement",
            "--width", "8",
            "--height", "6",
            "--default-terrain", "synthetic:plain",
            "--force");

        Assert.Equal(ScenarioEditorCommand.SuccessExitCode, result.ExitCode);
        Assert.Equal("Replacement", store.Load(path).Scenario!.Title);
    }

    [Fact]
    public void Validate_ValidScenario_ReturnsSuccess()
    {
        string path = CreateFile("valid.json");

        CommandResult result = Run("validate", path);

        Assert.Equal(ScenarioEditorCommand.SuccessExitCode, result.ExitCode);
        Assert.Contains("Native scenario is valid.", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_InvalidJson_ReturnsValidationError()
    {
        string path = Path.Combine(directory, "invalid.json");
        File.WriteAllText(path, "invalid");

        CommandResult result = Run("validate", path);

        Assert.Equal(ScenarioEditorCommand.ValidationErrorExitCode, result.ExitCode);
        Assert.Contains("Code: InvalidJson", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_MissingFile_ReturnsInputError()
    {
        CommandResult result = Run("validate", Path.Combine(directory, "missing.json"));

        Assert.Equal(ScenarioEditorCommand.InputErrorExitCode, result.ExitCode);
        Assert.Contains("Code: FileNotFound", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Summary_ValidScenario_ReportsSafeMetadata()
    {
        string path = CreateFile("summary.json");

        CommandResult result = Run("summary", path);

        Assert.Equal(ScenarioEditorCommand.SuccessExitCode, result.ExitCode);
        Assert.Contains("Scenario ID: synthetic", result.Output, StringComparison.Ordinal);
        Assert.Contains("Map: 8 x 6", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(directory, result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PaintTerrain_AddsAndReplacesOverride()
    {
        string path = CreateFile("paint.json");

        Assert.Equal(0, Run("paint-terrain", path, "2", "3", "synthetic:forest").ExitCode);
        Assert.Equal(0, Run("paint-terrain", path, "2", "3", "synthetic:water").ExitCode);

        ScenarioDefinition scenario = store.Load(path).Scenario!;
        TerrainPlacement placement = Assert.Single(scenario.Map.Terrain);
        Assert.Equal("synthetic:water", placement.Terrain);
        Assert.Equal(2, placement.Position.X);
        Assert.Equal(3, placement.Position.Y);
    }

    [Fact]
    public void PaintTerrain_WithDefaultTerrain_RemovesOverride()
    {
        string path = CreateFile("paint.json");
        Assert.Equal(0, Run("paint-terrain", path, "2", "3", "synthetic:forest").ExitCode);

        CommandResult result = Run("paint-terrain", path, "2", "3", "synthetic:plain");

        Assert.Equal(ScenarioEditorCommand.SuccessExitCode, result.ExitCode);
        Assert.Empty(store.Load(path).Scenario!.Map.Terrain);
    }

    [Fact]
    public void PaintTerrain_OutsideMap_DoesNotModifyInput()
    {
        string path = CreateFile("paint.json");
        byte[] before = File.ReadAllBytes(path);

        CommandResult result = Run("paint-terrain", path, "8", "0", "synthetic:forest");

        Assert.Equal(ScenarioEditorCommand.ValidationErrorExitCode, result.ExitCode);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void PaintTerrain_WithOutput_PreservesInputAndCreatesOutput()
    {
        string inputPath = CreateFile("input.json");
        string outputPath = Path.Combine(directory, "output.json");
        byte[] before = File.ReadAllBytes(inputPath);

        CommandResult result = Run(
            "paint-terrain", inputPath, "1", "1", "synthetic:forest", "--output", outputPath);

        Assert.Equal(ScenarioEditorCommand.SuccessExitCode, result.ExitCode);
        Assert.Equal(before, File.ReadAllBytes(inputPath));
        Assert.Single(store.Load(outputPath).Scenario!.Map.Terrain);
    }

    [Fact]
    public void ObjectCommands_PlaceMoveAndRemoveObject()
    {
        string path = CreateFile("objects.json");

        CommandResult place = Run("place-object", path, "landmark", "synthetic:landmark", "2", "3");
        CommandResult move = Run("move-object", path, "landmark", "4", "5");

        Assert.Equal(ScenarioEditorCommand.SuccessExitCode, place.ExitCode);
        Assert.Equal(ScenarioEditorCommand.SuccessExitCode, move.ExitCode);
        ScenarioObjectPlacement placement = Assert.Single(store.Load(path).Scenario!.Map.Objects);
        Assert.Equal(4, placement.Position.X);
        Assert.Equal(5, placement.Position.Y);

        CommandResult remove = Run("remove-object", path, "landmark");
        Assert.Equal(ScenarioEditorCommand.SuccessExitCode, remove.ExitCode);
        Assert.Empty(store.Load(path).Scenario!.Map.Objects);
    }

    [Fact]
    public void PlaceObject_WithDuplicateId_DoesNotModifyInput()
    {
        string path = CreateFile("objects.json");
        Assert.Equal(0, Run("place-object", path, "landmark", "synthetic:landmark", "2", "3").ExitCode);
        byte[] before = File.ReadAllBytes(path);

        CommandResult result = Run("place-object", path, "landmark", "synthetic:other", "1", "1");

        Assert.Equal(ScenarioEditorCommand.ValidationErrorExitCode, result.ExitCode);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void PlaceObject_OutsideMap_DoesNotModifyInput()
    {
        string path = CreateFile("objects.json");
        byte[] before = File.ReadAllBytes(path);

        CommandResult result = Run("place-object", path, "landmark", "synthetic:landmark", "8", "0");

        Assert.Equal(ScenarioEditorCommand.ValidationErrorExitCode, result.ExitCode);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void ObjectCommands_MissingObject_ReturnValidationError()
    {
        string path = CreateFile("objects.json");

        Assert.Equal(ScenarioEditorCommand.ValidationErrorExitCode, Run("move-object", path, "missing", "1", "1").ExitCode);
        Assert.Equal(ScenarioEditorCommand.ValidationErrorExitCode, Run("remove-object", path, "missing").ExitCode);
    }

    [Fact]
    public void PlaceObject_WithOutput_PreservesInput()
    {
        string inputPath = CreateFile("objects.json");
        string outputPath = Path.Combine(directory, "objects-output.json");
        byte[] before = File.ReadAllBytes(inputPath);

        CommandResult result = Run(
            "place-object", inputPath, "landmark", "synthetic:landmark", "2", "3", "--output", outputPath);

        Assert.Equal(ScenarioEditorCommand.SuccessExitCode, result.ExitCode);
        Assert.Equal(before, File.ReadAllBytes(inputPath));
        Assert.Single(store.Load(outputPath).Scenario!.Map.Objects);
    }

    [Theory]
    [InlineData()]
    [InlineData("unknown")]
    [InlineData("create", "file.json")]
    [InlineData("paint-terrain", "file.json", "x", "0", "synthetic:plain")]
    public void InvalidArguments_ReturnUsageError(params string[] arguments)
    {
        CommandResult result = Run(arguments);

        Assert.Equal(ScenarioEditorCommand.UsageErrorExitCode, result.ExitCode);
        Assert.Contains("Usage:", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Errors_DoNotLeakAbsolutePath()
    {
        string path = Path.Combine(directory, "missing.json");

        CommandResult result = Run("validate", path);

        Assert.DoesNotContain(directory, result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("missing.json", result.Error, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        Directory.Delete(directory, true);
        GC.SuppressFinalize(this);
    }

    private string CreateFile(string name, string title = "Synthetic")
    {
        string path = Path.Combine(directory, name);
        var scenario = new ScenarioDefinition(
            ScenarioFormatV1.Version,
            "synthetic",
            title,
            null,
            new ScenarioMapDefinition(8, 6, "synthetic:plain", []));
        Assert.True(store.Save(path, scenario).IsSuccess);
        return path;
    }

    private CommandResult Run(params string[] arguments)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        int exitCode = ScenarioEditorCommand.Run(arguments, store, output, error);
        return new CommandResult(exitCode, output.ToString(), error.ToString());
    }

    private sealed record CommandResult(int ExitCode, string Output, string Error);
}
