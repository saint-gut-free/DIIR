using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Godot;
using DisciplesRemaster.Persistence.Projects;

namespace DisciplesRemaster.Game.Tests;

public sealed class HeadlessProjectCommandTests
{
    [Fact]
    public void ValidateProject_ValidProject_IsReadyWithoutPathLeakage()
    {
        string privateDirectory = Path.Combine(Path.GetTempPath(), "d2r-host-private");
        string path = Path.Combine(privateDirectory, "minimal.project.json");
        NativeProjectSceneLoader loader = CreateLoader(
            new NativeProjectLoadResult(NativeProjectSceneLoaderTests.CreateProject(includeSession: true), []));

        CommandResult result = Run(loader, "validate-project", path);

        Assert.Equal(HeadlessGameCommand.SuccessExitCode, result.ExitCode);
        Assert.Contains("ready for an outer host", result.Output, StringComparison.Ordinal);
        Assert.Contains("File: minimal.project.json", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(privateDirectory, result.AllOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SummaryProject_WithSession_ReportsSceneAndRuntimeCounts()
    {
        NativeProjectSceneLoader loader = CreateLoader(
            new NativeProjectLoadResult(NativeProjectSceneLoaderTests.CreateProject(includeSession: true), []));

        CommandResult result = Run(loader, "summary-project", "minimal.project.json");

        Assert.Equal(HeadlessGameCommand.SuccessExitCode, result.ExitCode);
        Assert.Contains("Scenario ID: synthetic-scenario", result.Output, StringComparison.Ordinal);
        Assert.Contains("Map: 8 x 6", result.Output, StringComparison.Ordinal);
        Assert.Contains("Content packages: 1", result.Output, StringComparison.Ordinal);
        Assert.Contains("Runtime checkpoint: valid", result.Output, StringComparison.Ordinal);
        Assert.Contains("Active participant: blue", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderProject_ValidViewport_ProducesBoundedDiagnosticViewWithoutPathLeakage()
    {
        string privateDirectory = Path.Combine(Path.GetTempPath(), "d2r-render-private");
        string path = Path.Combine(privateDirectory, "minimal.project.json");
        NativeProjectSceneLoader loader = CreateLoader(
            new NativeProjectLoadResult(NativeProjectSceneLoaderTests.CreateProject(includeSession: true), []));

        CommandResult result = Run(
            loader,
            "render-project",
            path,
            "--origin-x",
            "2",
            "--origin-y",
            "1",
            "--width",
            "3",
            "--height",
            "2");

        Assert.Equal(HeadlessGameCommand.SuccessExitCode, result.ExitCode);
        Assert.Contains("Native project diagnostic viewport", result.Output, StringComparison.Ordinal);
        Assert.Contains("Origin: 2,1", result.Output, StringComparison.Ordinal);
        Assert.Contains("Viewport: 3 x 2", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(privateDirectory, result.AllOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RenderProject_OriginOutsideMap_ReturnsValidationError()
    {
        NativeProjectSceneLoader loader = CreateLoader(
            new NativeProjectLoadResult(NativeProjectSceneLoaderTests.CreateProject(includeSession: false), []));

        CommandResult result = Run(loader, "render-project", "project.json", "--origin-x", "8");

        Assert.Equal(HeadlessGameCommand.ValidationErrorExitCode, result.ExitCode);
        Assert.Contains("OriginOutsideMap", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("FileNotFound", HeadlessGameCommand.InputErrorExitCode)]
    [InlineData("ValidationFailed", HeadlessGameCommand.ValidationErrorExitCode)]
    [InlineData("UnexpectedError", HeadlessGameCommand.SoftwareErrorExitCode)]
    public void ProjectFailure_ReturnsStableExitCode(string detailCode, int expectedExitCode)
    {
        var issue = new NativeProjectLoadIssue(
            NativeProjectLoadIssueCode.ManifestLoadFailed,
            "manifest",
            "$",
            detailCode,
            "Safe failure.");
        NativeProjectSceneLoader loader = CreateLoader(new NativeProjectLoadResult(null, [issue]));

        CommandResult result = Run(loader, "validate-project", "project.json");

        Assert.Equal(expectedExitCode, result.ExitCode);
        Assert.Contains(detailCode, result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData()]
    [InlineData("unknown")]
    [InlineData("validate-project")]
    [InlineData("summary-project", "one", "two")]
    [InlineData("render-project")]
    [InlineData("render-project", "project.json", "--width", "0")]
    [InlineData("render-project", "project.json", "--height", "61")]
    [InlineData("render-project", "project.json", "--width", "10", "--width", "20")]
    [InlineData("render-project", "project.json", "--unknown", "1")]
    public void InvalidArguments_ReturnUsageError(params string[] arguments)
    {
        NativeProjectSceneLoader loader = CreateLoader(
            new NativeProjectLoadResult(NativeProjectSceneLoaderTests.CreateProject(includeSession: false), []));

        CommandResult result = Run(loader, arguments);

        Assert.Equal(HeadlessGameCommand.UsageErrorExitCode, result.ExitCode);
        Assert.Contains("Usage:", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void UnexpectedFailure_ReturnsSoftwareErrorWithoutStackTrace()
    {
        var loader = new NativeProjectSceneLoader(new ThrowingProjectLoader(), new ScenarioValidationService());

        CommandResult result = Run(loader, "validate-project", "project.json");

        Assert.Equal(HeadlessGameCommand.SoftwareErrorExitCode, result.ExitCode);
        Assert.Contains("Code: UnexpectedError", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("System.", result.Error, StringComparison.Ordinal);
    }

    private static NativeProjectSceneLoader CreateLoader(NativeProjectLoadResult result) =>
        new(new NativeProjectSceneLoaderTests.StubProjectLoader(result), new ScenarioValidationService());

    private static CommandResult Run(NativeProjectSceneLoader loader, params string[] arguments)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        int exitCode = HeadlessProjectCommand.Run(arguments, loader, output, error);
        return new CommandResult(exitCode, output.ToString(), error.ToString());
    }

    private sealed record CommandResult(int ExitCode, string Output, string Error)
    {
        public string AllOutput => Output + Error;
    }

    private sealed class ThrowingProjectLoader : INativeProjectLoader
    {
        public NativeProjectLoadResult Load(string manifestPath) => throw new IOException("Synthetic failure.");
    }
}
