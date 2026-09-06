using DisciplesRemaster.Content.Catalog;
using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Editor;
using DisciplesRemaster.Persistence.Content;
using DisciplesRemaster.Persistence.Scenarios;

namespace DisciplesRemaster.Editor.Tests;

public sealed class ContentEditorCommandTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"d2r-content-editor-{Guid.NewGuid():N}");
    private readonly ContentPackageValidationService contentValidation = new();
    private readonly ContentPackageFileStore contentStore;
    private readonly ScenarioFileStore scenarioStore;

    public ContentEditorCommandTests()
    {
        Directory.CreateDirectory(directory);
        contentStore = new ContentPackageFileStore(new ContentPackageJsonSerializer(contentValidation));
        scenarioStore = new ScenarioFileStore(new ScenarioJsonSerializer(new ScenarioValidationService()));
    }

    [Fact]
    public void ValidateContent_ValidPackage_ReturnsSuccess()
    {
        string path = SavePackage();

        CommandResult result = Run("validate-content", path);

        Assert.Equal(ScenarioEditorCommand.SuccessExitCode, result.ExitCode);
        Assert.Contains("Package ID: synthetic", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(directory, result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateContent_InvalidPackage_ReturnsValidationError()
    {
        string path = Path.Combine(directory, "invalid.json");
        File.WriteAllText(path, "not-json");

        CommandResult result = Run("validate-content", path);

        Assert.Equal(ScenarioEditorCommand.ValidationErrorExitCode, result.ExitCode);
        Assert.Contains("Code: InvalidJson", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateScenarioContent_KnownReferences_ReturnsSuccess()
    {
        string packagePath = SavePackage();
        string scenarioPath = SaveScenario("synthetic:plain", "synthetic:landmark");

        CommandResult result = Run("validate-scenario-content", scenarioPath, packagePath);

        Assert.Equal(ScenarioEditorCommand.SuccessExitCode, result.ExitCode);
        Assert.Contains("Packages: 1", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateScenarioContent_UnknownReferences_ReturnsTypedIssues()
    {
        string packagePath = SavePackage();
        string scenarioPath = SaveScenario("missing:plain", "missing:landmark");

        CommandResult result = Run("validate-scenario-content", scenarioPath, packagePath);

        Assert.Equal(ScenarioEditorCommand.ValidationErrorExitCode, result.ExitCode);
        Assert.Contains("UnknownDefaultTerrain", result.Error, StringComparison.Ordinal);
        Assert.Contains("UnknownObjectArchetype", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(directory, result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateScenarioContent_DuplicatePackages_ReturnsValidationError()
    {
        string packagePath = SavePackage();
        string copyPath = Path.Combine(directory, "copy.json");
        File.Copy(packagePath, copyPath);
        string scenarioPath = SaveScenario("synthetic:plain", "synthetic:landmark");

        CommandResult result = Run("validate-scenario-content", scenarioPath, packagePath, copyPath);

        Assert.Equal(ScenarioEditorCommand.ValidationErrorExitCode, result.ExitCode);
        Assert.Contains("DuplicatePackageId", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("validate-content")]
    [InlineData("validate-scenario-content", "scenario.json")]
    [InlineData("unknown-content")]
    public void InvalidArguments_ReturnUsageError(params string[] arguments)
    {
        CommandResult result = Run(arguments);

        Assert.Equal(ScenarioEditorCommand.UsageErrorExitCode, result.ExitCode);
        Assert.Contains("Usage:", result.Error, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        Directory.Delete(directory, true);
        GC.SuppressFinalize(this);
    }

    private string SavePackage()
    {
        string path = Path.Combine(directory, "synthetic.package.json");
        var package = new ContentPackageDefinition(
            ContentPackageFormatV1.Version,
            "synthetic",
            "Synthetic",
            [new TerrainContentDefinition("plain", "Plain")],
            [new ObjectArchetypeDefinition("landmark", "Landmark")]);
        Assert.True(contentStore.Save(path, package).IsSuccess);
        return path;
    }

    private string SaveScenario(string terrain, string archetype)
    {
        string path = Path.Combine(directory, $"scenario-{Guid.NewGuid():N}.json");
        var scenario = new ScenarioDefinition(
            ScenarioFormatV1.Version,
            "synthetic",
            "Synthetic",
            null,
            new ScenarioMapDefinition(4, 4, terrain, [])
            {
                Objects = [new ScenarioObjectPlacement("object", archetype, new GridPosition(1, 1))],
            });
        Assert.True(scenarioStore.Save(path, scenario).IsSuccess);
        return path;
    }

    private CommandResult Run(params string[] arguments)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        int exitCode = ContentEditorCommand.Run(
            arguments,
            contentStore,
            scenarioStore,
            contentValidation,
            output,
            error);
        return new CommandResult(exitCode, output.ToString(), error.ToString());
    }

    private sealed record CommandResult(int ExitCode, string Output, string Error);
}
