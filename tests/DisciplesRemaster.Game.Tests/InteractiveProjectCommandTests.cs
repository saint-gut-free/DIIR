using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Core.Movement;
using DisciplesRemaster.Core.Navigation;
using DisciplesRemaster.Core.Sessions;
using DisciplesRemaster.Godot;
using DisciplesRemaster.Persistence.Projects;

namespace DisciplesRemaster.Game.Tests;

public sealed class InteractiveProjectCommandTests
{
    [Fact]
    public void Transcript_SelectPreviewConfirmAndTurn_UsesInMemoryRuntime()
    {
        NativeProject project = Project();
        const string transcript = "select blue-actor\npreview 2 1\nconfirm\nend-turn\nselect red-actor\npreview 5 4\nconfirm\nshow\nquit\n";

        CommandResult first = Run(project, transcript);
        CommandResult second = Run(project, transcript);

        Assert.Equal(0, first.ExitCode);
        Assert.Empty(first.Error);
        Assert.Equal(first, second);
        Assert.Contains("Code: MovementPreviewReady", first.Output, StringComparison.Ordinal);
        Assert.Contains("Code: MovementApplied", first.Output, StringComparison.Ordinal);
        Assert.Contains("position: (2, 1); movement: 3/4", first.Output, StringComparison.Ordinal);
        Assert.Contains("active participant: red", first.Output, StringComparison.Ordinal);
        Assert.Contains("position: (5, 4); movement: 2/3", first.Output, StringComparison.Ordinal);
        Assert.Equal(new GridPosition(1, 1), project.Session!.Actors["blue-actor"].Position);
        Assert.Equal("blue", project.Session.Turn.ActiveParticipantId);
    }

    [Fact]
    public void Transcript_InvalidInputAndRejectedActions_AllowRecovery()
    {
        CommandResult result = Run(Project(),
            "select red-actor\npreview 5 4\nconfirm\npreview nope 4\nselect blue-actor\npreview 100 100\npreview 2 1\nconfirm\nquit\n");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("ActorNotControlledByActiveParticipant", result.Output, StringComparison.Ordinal);
        Assert.Contains("Code: NoMovementPreview", result.Output, StringComparison.Ordinal);
        Assert.Contains("DestinationOutsideGrid", result.Output, StringComparison.Ordinal);
        Assert.Contains("Code: InvalidInteractiveCommand", result.Error, StringComparison.Ordinal);
        Assert.Contains("position: (2, 1); movement: 3/4", result.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("preview nope 4")]
    [InlineData("preview 2")]
    [InlineData("preview 2 1 extra")]
    [InlineData("preview 2147483648 1")]
    public void Transcript_InvalidPreviewArguments_CancelPreviouslyValidMovement(string invalidPreview)
    {
        NativeProject project = Project();

        CommandResult result = Run(project,
            $"select blue-actor\npreview 2 1\n{invalidPreview}\nconfirm\nquit\n");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Code: MovementPreviewReady", result.Output, StringComparison.Ordinal);
        Assert.Contains("Code: InvalidInteractiveCommand", result.Error, StringComparison.Ordinal);
        Assert.Contains("Code: NoMovementPreview", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Code: MovementApplied", result.Output, StringComparison.Ordinal);
        string confirmation = result.Output[(result.Output.IndexOf("Code: NoMovementPreview", StringComparison.Ordinal))..];
        Assert.Contains("Actor: blue-actor; owner: blue; position: (1, 1); movement: 4/4", confirmation, StringComparison.Ordinal);
        Assert.Equal(new GridPosition(1, 1), project.Session!.Actors["blue-actor"].Position);
        Assert.Equal(4, project.Session.Actors["blue-actor"].RemainingMovement);
    }

    [Fact]
    public void Actors_OverListingLimit_ListsFirstTwoHundredInStableOrderAndAllowsUnlistedSelection()
    {
        NativeProject project = Project();
        GameSessionState session = GameSessionState.Create(new GridSize(8, 6), ["blue"],
            Enumerable.Range(0, 201).Reverse().Select(index =>
                new GameActorDefinition($"actor-{index:000}", "blue", new GridPosition(0, 0), 4))).Session!;
        project = project with { Session = session };

        CommandResult listing = Run(project, "actors\nquit\n");
        CommandResult selection = Run(project, "select actor-200\npreview 1 0\nconfirm\nquit\n");

        Assert.Equal(0, listing.ExitCode);
        Assert.Empty(listing.Error);
        Assert.Equal(200, listing.Output.Split("Actor: ", StringSplitOptions.None).Length - 1);
        Assert.Contains("Actor: actor-000;", listing.Output, StringComparison.Ordinal);
        Assert.Contains("Actor: actor-199;", listing.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Actor: actor-200;", listing.Output, StringComparison.Ordinal);
        Assert.Contains("Actor list truncated.", listing.Output, StringComparison.Ordinal);
        Assert.True(listing.Output.IndexOf("Actor: actor-000;", StringComparison.Ordinal) <
            listing.Output.IndexOf("Actor: actor-199;", StringComparison.Ordinal));
        Assert.Equal(listing, Run(project, "actors\nquit\n"));
        Assert.Equal(0, selection.ExitCode);
        Assert.Empty(selection.Error);
        Assert.Contains("Code: ActorSelected", selection.Output, StringComparison.Ordinal);
        Assert.Contains("Code: MovementApplied", selection.Output, StringComparison.Ordinal);
        Assert.Contains("Actor: actor-200; owner: blue; position: (1, 0); movement: 3/4", selection.Output, StringComparison.Ordinal);
        Assert.Equal(new GridPosition(0, 0), session.Actors["actor-200"].Position);
    }

    [Fact]
    public void Preview_LongRoute_BoundsDisplayedPositionsWithoutTruncatingActualMovement()
    {
        NativeProject project = Project();
        GameSessionState session = GameSessionState.Create(new GridSize(40, 1), ["blue"],
            [new GameActorDefinition("blue-actor", "blue", new GridPosition(0, 0), 39)]).Session!;
        ScenarioDefinition scenario = project.ScenarioBundle.Scenario with
        {
            Map = new ScenarioMapDefinition(40, 1, "synthetic:plain", []),
        };
        project = project with
        {
            ScenarioBundle = project.ScenarioBundle with { Scenario = scenario },
            Session = session,
        };

        CommandResult result = Run(project, "select blue-actor\npreview 39 0\nconfirm\nquit\n");

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        string[] routeLines = result.Output.Split('\n')
            .Where(line => line.StartsWith("Route: ", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, routeLines.Length);
        Assert.All(routeLines, route =>
        {
            Assert.Equal(32, route.Split(" -> ", StringSplitOptions.None).Length);
            Assert.StartsWith("Route: (0, 0) -> (1, 0)", route, StringComparison.Ordinal);
            Assert.Contains("(31, 0)", route, StringComparison.Ordinal);
            Assert.DoesNotContain("(32, 0)", route, StringComparison.Ordinal);
            Assert.DoesNotContain("(39, 0)", route, StringComparison.Ordinal);
        });
        Assert.Equal(2, result.Output.Split("Route display truncated.", StringSplitOptions.None).Length - 1);
        Assert.Contains("Steps: 39; budget: 39", result.Output, StringComparison.Ordinal);
        Assert.Contains("Code: MovementApplied", result.Output, StringComparison.Ordinal);
        Assert.Contains("Actor: blue-actor; owner: blue; position: (39, 0); movement: 0/39", result.Output, StringComparison.Ordinal);
        Assert.Equal(new GridPosition(0, 0), session.Actors["blue-actor"].Position);
        Assert.Equal(39, session.Actors["blue-actor"].RemainingMovement);
    }

    [Theory]
    [InlineData("")]
    [InlineData("status")]
    [InlineData("status\rquit\r")]
    [InlineData("status\r\nquit\r\n")]
    public void EndOfInputAndCommonLineEndings_ExitNormally(string transcript)
    {
        CommandResult result = Run(Project(), transcript);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Session ended.", result.Output, StringComparison.Ordinal);
        Assert.Empty(result.Error);
    }

    [Fact]
    public void OversizeCommand_IsDiscardedAndNextCommandRuns()
    {
        CommandResult result = Run(Project(), new string('x', InteractiveProjectCommand.MaximumCommandLength + 1) + "\nstatus\nquit\n");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Code: CommandTooLong", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('x', 80), result.Error, StringComparison.Ordinal);
        Assert.Contains("Session ended.", result.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("preview 3 1 ")]
    [InlineData("status ")]
    public void OversizeCommand_CancelsPendingPreviewWithoutApplyingMovement(string commandPrefix)
    {
        NativeProject project = Project();
        string oversized = commandPrefix + new string('x', InteractiveProjectCommand.MaximumCommandLength);

        CommandResult result = Run(project,
            $"select blue-actor\npreview 2 1\n{oversized}\nconfirm\nquit\n");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Code: MovementPreviewReady", result.Output, StringComparison.Ordinal);
        Assert.Contains("Code: CommandTooLong", result.Error, StringComparison.Ordinal);
        Assert.Contains("Code: NoMovementPreview", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Code: MovementApplied", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('x', 80), result.Output + result.Error, StringComparison.Ordinal);
        string confirmation = result.Output[(result.Output.IndexOf("Code: NoMovementPreview", StringComparison.Ordinal))..];
        Assert.Contains("Actor: blue-actor; owner: blue; position: (1, 1); movement: 4/4", confirmation, StringComparison.Ordinal);
        Assert.Equal(new GridPosition(1, 1), project.Session!.Actors["blue-actor"].Position);
        Assert.Equal(4, project.Session.Actors["blue-actor"].RemainingMovement);
    }

    [Fact]
    public void ViewportRequest_UpdatesOnlyForValidDimensions()
    {
        CommandResult result = Run(Project(), "show 0 0 2 2\nshow 0 0 999 2\nshow\nquit\n");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(2, result.Output.Split("Viewport: 2 x 2", StringSplitOptions.None).Length - 1);
        Assert.Contains("Code: InvalidViewportWidth", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingRuntimeCheckpoint_IsValidationError()
    {
        NativeProject project = NativeProjectSceneLoaderTests.CreateProject(includeSession: false);

        CommandResult result = Run(project, "quit\n");

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("SessionUnavailable", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("FileNotFound", 2)]
    [InlineData("FileInaccessible", 2)]
    [InlineData("ValidationFailed", 3)]
    [InlineData("UnexpectedError", 70)]
    public void LoadFailure_IsReportedWithoutAbsoluteInputPath(string detail, int expectedExit)
    {
        var load = new NativeProjectLoadResult(null,
        [
            new NativeProjectLoadIssue(NativeProjectLoadIssueCode.ManifestLoadFailed,
                "manifest", "$", detail, "Synthetic load failure."),
        ]);
        string privateRoot = Path.Combine(Path.GetTempPath(), "synthetic-interactive-private");

        CommandResult result = Run(load, "", "play-open-grid", Path.Combine(privateRoot, "project.json"));

        Assert.Equal(expectedExit, result.ExitCode);
        Assert.DoesNotContain(privateRoot, result.Output + result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData()]
    [InlineData("unknown")]
    [InlineData("play-open-grid")]
    [InlineData("play-open-grid", "project.json", "extra")]
    public void InvalidTopLevelArguments_ReturnUsageError(params string[] arguments)
    {
        CommandResult result = Run(new NativeProjectLoadResult(Project(), []), "", arguments);

        Assert.Equal(64, result.ExitCode);
    }

    [Fact]
    public void SelectionIdWithSpacesAndControls_HasReadableEscapedOutput()
    {
        NativeProject project = Project();
        GameSessionState session = GameSessionState.Create(new GridSize(8, 6), ["blue"],
            [new GameActorDefinition("actor with spaces\u001b", "blue", new GridPosition(0, 0), 4)]).Session!;

        CommandResult result = Run(project with { Session = session }, "select actor with spaces\u001b\nstatus\nquit\n");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Code: ActorSelected", result.Output, StringComparison.Ordinal);
        Assert.Contains("actor with spaces?", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain('\u001b', result.Output);
    }

    private static NativeProject Project()
    {
        NativeProject project = NativeProjectSceneLoaderTests.CreateProject(includeSession: true);
        GameSessionState session = GameSessionState.Create(new GridSize(8, 6), ["blue", "red"],
        [
            new GameActorDefinition("blue-actor", "blue", new GridPosition(1, 1), 4),
            new GameActorDefinition("red-actor", "red", new GridPosition(6, 4), 3),
        ]).Session!;
        return project with { Session = session };
    }

    private static CommandResult Run(NativeProject project, string transcript) =>
        Run(new NativeProjectLoadResult(project, []), transcript, "play-open-grid", "project.json");

    private static CommandResult Run(NativeProjectLoadResult load, string transcript, params string[] arguments)
    {
        var loader = new NativeProjectSceneLoader(new NativeProjectSceneLoaderTests.StubProjectLoader(load),
            new ScenarioValidationService());
        using var input = new StringReader(transcript);
        using var output = new StringWriter();
        using var error = new StringWriter();
        int code = InteractiveProjectCommand.Run(arguments, loader,
            new GameSessionService(new MovementPlanner(new GridPathfinder())), input, output, error);
        return new CommandResult(code, output.ToString(), error.ToString());
    }

    private sealed record CommandResult(int ExitCode, string Output, string Error);
}
