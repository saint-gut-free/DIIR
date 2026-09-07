using DisciplesRemaster.Content.Catalog;
using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Editor;
using DisciplesRemaster.Persistence.Projects;

namespace DisciplesRemaster.Editor.Tests;

public sealed class ProjectEditorCommandTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"d2r-project-editor-{Guid.NewGuid():N}");
    private readonly NativeProjectManifestFileStore manifestStore;

    public ProjectEditorCommandTests()
    {
        Directory.CreateDirectory(directory);
        manifestStore = new NativeProjectManifestFileStore(
            new NativeProjectManifestJsonSerializer(new NativeProjectManifestValidationService()));
    }

    [Fact]
    public void CreateProject_ValidOptions_WritesPortableDeterministicManifest()
    {
        string path = Path.Combine(directory, "created.project.json");

        CommandResult result = Run(
            new StubLoader(new NativeProjectLoadResult(CreateProject(), [])),
            "create-project",
            path,
            "--id",
            "created-project",
            "--scenario",
            "scenarios/main.json",
            "--content",
            "content/z.json",
            "--content",
            "content/a.json",
            "--session",
            "sessions/current.json");

        Assert.Equal(ScenarioEditorCommand.SuccessExitCode, result.ExitCode);
        Assert.DoesNotContain(directory, result.Output, StringComparison.OrdinalIgnoreCase);
        NativeProjectManifestLoadResult load = manifestStore.Load(path);
        Assert.True(load.IsSuccess);
        Assert.Equal("created-project", load.Manifest!.Id);
        Assert.Equal(["content/a.json", "content/z.json"], load.Manifest.ContentPackages);
        Assert.Equal("sessions/current.json", load.Manifest.Session);
    }

    [Fact]
    public void CreateProject_ExistingOutput_RequiresExplicitForce()
    {
        string path = Path.Combine(directory, "existing.project.json");
        File.WriteAllText(path, "preserve-me");

        CommandResult protectedResult = RunCreate(path);
        string protectedContent = File.ReadAllText(path);
        CommandResult forcedResult = RunCreate(path, "--force");

        Assert.Equal(ScenarioEditorCommand.InputErrorExitCode, protectedResult.ExitCode);
        Assert.Contains("OutputAlreadyExists", protectedResult.Error, StringComparison.Ordinal);
        Assert.Equal("preserve-me", protectedContent);
        Assert.Equal(ScenarioEditorCommand.SuccessExitCode, forcedResult.ExitCode);
        Assert.True(manifestStore.Load(path).IsSuccess);
    }

    [Fact]
    public void CreateProject_UnsafeReference_ReturnsValidationErrorWithoutFile()
    {
        string path = Path.Combine(directory, "unsafe.project.json");

        CommandResult result = Run(
            new StubLoader(new NativeProjectLoadResult(CreateProject(), [])),
            "create-project",
            path,
            "--id",
            "unsafe",
            "--scenario",
            "../outside.json",
            "--content",
            "content/package.json");

        Assert.Equal(ScenarioEditorCommand.ValidationErrorExitCode, result.ExitCode);
        Assert.Contains(nameof(NativeProjectManifestValidationCode.PathEscapesProjectDirectory), result.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void ValidateProject_ValidProject_ReturnsSuccessWithoutPaths()
    {
        NativeProject project = CreateProject();
        var loader = new StubLoader(new NativeProjectLoadResult(project, []));
        string privateDirectory = Path.Combine(Path.GetTempPath(), "d2r-private");
        string privatePath = Path.Combine(privateDirectory, "minimal.project.json");

        CommandResult result = Run(loader, "validate-project", privatePath);

        Assert.Equal(ScenarioEditorCommand.SuccessExitCode, result.ExitCode);
        Assert.Contains("Project ID: synthetic-project", result.Output, StringComparison.Ordinal);
        Assert.Contains("File: minimal.project.json", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(privateDirectory, result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SummaryProject_ValidProject_ReportsResolvedComponents()
    {
        var loader = new StubLoader(new NativeProjectLoadResult(CreateProject(), []));

        CommandResult result = Run(loader, "summary-project", "minimal.project.json");

        Assert.Equal(ScenarioEditorCommand.SuccessExitCode, result.ExitCode);
        Assert.Contains("Scenario ID: synthetic-scenario", result.Output, StringComparison.Ordinal);
        Assert.Contains("Map: 8 x 6", result.Output, StringComparison.Ordinal);
        Assert.Contains("Content packages: 1", result.Output, StringComparison.Ordinal);
        Assert.Contains("- synthetic", result.Output, StringComparison.Ordinal);
        Assert.Contains("Session checkpoint: not configured", result.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("FileNotFound", ScenarioEditorCommand.InputErrorExitCode)]
    [InlineData("ValidationFailed", ScenarioEditorCommand.ValidationErrorExitCode)]
    [InlineData("UnexpectedError", ScenarioEditorCommand.SoftwareErrorExitCode)]
    public void LoadFailure_ReturnsStableExitCode(string detailCode, int expectedExitCode)
    {
        var issue = new NativeProjectLoadIssue(
            NativeProjectLoadIssueCode.ManifestLoadFailed,
            "manifest",
            "$",
            detailCode,
            "Safe failure.");
        var loader = new StubLoader(new NativeProjectLoadResult(null, [issue]));
        string privateDirectory = Path.Combine(Path.GetTempPath(), "d2r-private");
        string privatePath = Path.Combine(privateDirectory, "invalid.project.json");

        CommandResult result = Run(loader, "validate-project", privatePath);

        Assert.Equal(expectedExitCode, result.ExitCode);
        Assert.DoesNotContain(privateDirectory, result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(detailCode, result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData()]
    [InlineData("unknown")]
    [InlineData("create-project")]
    [InlineData("create-project", "project.json")]
    [InlineData("create-project", "project.json", "--id", "id", "--scenario", "scenario.json")]
    [InlineData("create-project", "project.json", "--unknown", "value")]
    [InlineData("validate-project")]
    [InlineData("summary-project", "one", "two")]
    public void InvalidArguments_ReturnUsageError(params string[] arguments)
    {
        CommandResult result = Run(new StubLoader(new NativeProjectLoadResult(CreateProject(), [])), arguments);

        Assert.Equal(ScenarioEditorCommand.UsageErrorExitCode, result.ExitCode);
        Assert.Contains("Usage:", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void UnexpectedException_ReturnsSoftwareErrorWithoutStackTrace()
    {
        CommandResult result = Run(new ThrowingLoader(), "validate-project", "project.json");

        Assert.Equal(ScenarioEditorCommand.SoftwareErrorExitCode, result.ExitCode);
        Assert.Contains("Code: UnexpectedError", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("System.", result.Error, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        Directory.Delete(directory, true);
        GC.SuppressFinalize(this);
    }

    private static NativeProject CreateProject()
    {
        var package = new ContentPackageDefinition(
            ContentPackageFormatV1.Version,
            "synthetic",
            "Synthetic",
            [new TerrainContentDefinition("plain", "Plain")],
            []);
        ContentCatalog catalog = ContentCatalog.Build(
            [package],
            new ContentPackageValidationService()).Catalog!;
        var scenario = new ScenarioDefinition(
            ScenarioFormatV1.Version,
            "synthetic-scenario",
            "Synthetic scenario",
            null,
            new ScenarioMapDefinition(8, 6, "synthetic:plain", []));
        var manifest = new NativeProjectManifest(
            NativeProjectManifestFormatV1.Version,
            "synthetic-project",
            "scenarios/scenario.json",
            ["content/synthetic.json"],
            null);
        return new NativeProject(manifest, new ScenarioBundle(scenario, catalog, ["synthetic"]), null);
    }

    private CommandResult RunCreate(string path, params string[] suffix) =>
        Run(
            new StubLoader(new NativeProjectLoadResult(CreateProject(), [])),
            [
                "create-project",
                path,
                "--id",
                "created",
                "--scenario",
                "scenarios/main.json",
                "--content",
                "content/main.json",
                .. suffix,
            ]);

    private CommandResult Run(INativeProjectLoader loader, params string[] arguments)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        int exitCode = ProjectEditorCommand.Run(arguments, loader, manifestStore, output, error);
        return new CommandResult(exitCode, output.ToString(), error.ToString());
    }

    private sealed record CommandResult(int ExitCode, string Output, string Error);

    private sealed class StubLoader(NativeProjectLoadResult result) : INativeProjectLoader
    {
        public NativeProjectLoadResult Load(string manifestPath) => result;
    }

    private sealed class ThrowingLoader : INativeProjectLoader
    {
        public NativeProjectLoadResult Load(string manifestPath) => throw new IOException("Synthetic failure.");
    }
}
