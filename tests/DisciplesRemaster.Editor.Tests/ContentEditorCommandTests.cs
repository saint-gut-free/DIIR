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
    public void CreateContent_ValidArguments_WritesDeterministicPackage()
    {
        string first = Path.Combine(directory, "first.package.json");
        string second = Path.Combine(directory, "second.package.json");

        CommandResult firstResult = Run(
            "create-content",
            first,
            "--id",
            "synthetic-new",
            "--display-name",
            "Synthetic new package");
        CommandResult secondResult = Run(
            "create-content",
            second,
            "--display-name",
            "Synthetic new package",
            "--id",
            "synthetic-new");

        Assert.Equal(ScenarioEditorCommand.SuccessExitCode, firstResult.ExitCode);
        Assert.Equal(ScenarioEditorCommand.SuccessExitCode, secondResult.ExitCode);
        Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
        ContentPackageDefinition package = LoadPackage(first);
        Assert.Equal("synthetic-new", package.Id);
        Assert.Equal("Synthetic new package", package.DisplayName);
        Assert.Empty(package.Terrains);
        Assert.Empty(package.ObjectArchetypes);
        Assert.DoesNotContain(directory, firstResult.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateContent_ExistingOutput_RequiresForce()
    {
        string path = SavePackage();
        byte[] before = File.ReadAllBytes(path);

        CommandResult protectedResult = Run(
            "create-content",
            path,
            "--id",
            "replacement",
            "--display-name",
            "Replacement");

        Assert.Equal(ScenarioEditorCommand.InputErrorExitCode, protectedResult.ExitCode);
        Assert.Contains("OutputAlreadyExists", protectedResult.Error, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(path));

        CommandResult forcedResult = Run(
            "create-content",
            path,
            "--id",
            "replacement",
            "--display-name",
            "Replacement",
            "--force");

        Assert.Equal(ScenarioEditorCommand.SuccessExitCode, forcedResult.ExitCode);
        Assert.NotEqual(before, File.ReadAllBytes(path));
        Assert.Equal("replacement", LoadPackage(path).Id);
    }

    [Fact]
    public void CreateContent_InvalidPackageId_ReturnsValidationErrorWithoutFile()
    {
        string path = Path.Combine(directory, "invalid-id.package.json");

        CommandResult result = Run(
            "create-content",
            path,
            "--id",
            "Invalid ID",
            "--display-name",
            "Invalid");

        Assert.Equal(ScenarioEditorCommand.ValidationErrorExitCode, result.ExitCode);
        Assert.Contains("PackageIdInvalid", result.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void TerrainCommands_AddRenameAndRemoveTypedEntry()
    {
        string path = SavePackage();

        Assert.Equal(
            ScenarioEditorCommand.SuccessExitCode,
            Run("add-terrain", path, "water", "Water").ExitCode);
        Assert.Equal("Water", LoadPackage(path).Terrains.Single(item => item.Id == "water").DisplayName);

        Assert.Equal(
            ScenarioEditorCommand.SuccessExitCode,
            Run("set-terrain-name", path, "water", "Deep water").ExitCode);
        Assert.Equal("Deep water", LoadPackage(path).Terrains.Single(item => item.Id == "water").DisplayName);

        Assert.Equal(
            ScenarioEditorCommand.SuccessExitCode,
            Run("remove-terrain", path, "water").ExitCode);
        Assert.DoesNotContain(LoadPackage(path).Terrains, item => item.Id == "water");
    }

    [Fact]
    public void ObjectArchetypeCommands_AddRenameAndRemoveTypedEntry()
    {
        string path = SavePackage();

        Assert.Equal(
            ScenarioEditorCommand.SuccessExitCode,
            Run("add-object-archetype", path, "marker", "Marker").ExitCode);
        Assert.Equal(
            "Marker",
            LoadPackage(path).ObjectArchetypes.Single(item => item.Id == "marker").DisplayName);

        Assert.Equal(
            ScenarioEditorCommand.SuccessExitCode,
            Run("set-object-archetype-name", path, "marker", "Map marker").ExitCode);
        Assert.Equal(
            "Map marker",
            LoadPackage(path).ObjectArchetypes.Single(item => item.Id == "marker").DisplayName);

        Assert.Equal(
            ScenarioEditorCommand.SuccessExitCode,
            Run("remove-object-archetype", path, "marker").ExitCode);
        Assert.DoesNotContain(LoadPackage(path).ObjectArchetypes, item => item.Id == "marker");
    }

    [Fact]
    public void EditContent_RejectedOperationPreservesInput()
    {
        string path = SavePackage();
        byte[] before = File.ReadAllBytes(path);

        CommandResult duplicate = Run("add-terrain", path, "plain", "Duplicate");
        CommandResult missing = Run("remove-object-archetype", path, "missing");

        Assert.Equal(ScenarioEditorCommand.ValidationErrorExitCode, duplicate.ExitCode);
        Assert.Contains("DuplicateEntryId", duplicate.Error, StringComparison.Ordinal);
        Assert.Equal(ScenarioEditorCommand.ValidationErrorExitCode, missing.ExitCode);
        Assert.Contains("EntryNotFound", missing.Error, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void EditContent_OutputOptionPreservesInput()
    {
        string input = SavePackage();
        string output = Path.Combine(directory, "edited.package.json");
        byte[] before = File.ReadAllBytes(input);

        CommandResult result = Run(
            "set-content-name",
            input,
            "Edited package",
            "--output",
            output);

        Assert.Equal(ScenarioEditorCommand.SuccessExitCode, result.ExitCode);
        Assert.Equal(before, File.ReadAllBytes(input));
        Assert.Equal("Edited package", LoadPackage(output).DisplayName);
        Assert.DoesNotContain(directory, result.Output, StringComparison.OrdinalIgnoreCase);
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
    [InlineData("create-content", "package.json", "--id", "missing-name")]
    [InlineData("add-terrain", "package.json", "terrain")]
    [InlineData("remove-terrain", "package.json", "terrain", "--unknown", "output.json")]
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

    private ContentPackageDefinition LoadPackage(string path)
    {
        ContentPackageLoadResult result = contentStore.Load(path);
        Assert.True(result.IsSuccess);
        return Assert.IsType<ContentPackageDefinition>(result.Package);
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
