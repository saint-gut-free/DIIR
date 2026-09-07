using DisciplesRemaster.Content.Catalog;
using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Editor;
using DisciplesRemaster.Persistence.Projects;

namespace DisciplesRemaster.Editor.Tests;

public sealed class ProjectEditorCommandTests
{
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

    private static CommandResult Run(INativeProjectLoader loader, params string[] arguments)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        int exitCode = ProjectEditorCommand.Run(arguments, loader, output, error);
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
