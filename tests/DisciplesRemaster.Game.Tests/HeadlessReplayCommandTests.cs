using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Core.Movement;
using DisciplesRemaster.Core.Navigation;
using DisciplesRemaster.Core.Sessions;
using DisciplesRemaster.Godot;
using DisciplesRemaster.Persistence.Sessions;

namespace DisciplesRemaster.Game.Tests;

public sealed class HeadlessReplayCommandTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"d2r-replay-{Guid.NewGuid():N}");
    private readonly GameSessionFileStore sessionStore = new(new GameSessionJsonSerializer());
    private readonly GameSessionActionLogJsonSerializer actionSerializer = new();
    private readonly GameSessionActionProcessor processor = new(
        new GameSessionService(new MovementPlanner(new GridPathfinder())));

    public HeadlessReplayCommandTests()
    {
        Directory.CreateDirectory(directory);
    }

    [Fact]
    public void ReplayOpenGrid_ValidBatch_WritesDeterministicOutputAndPreservesInputs()
    {
        string checkpoint = SaveSession();
        string actions = SaveActions(CreateActions());
        string output = Path.Combine(directory, "replayed.json");
        byte[] checkpointBefore = File.ReadAllBytes(checkpoint);
        byte[] actionsBefore = File.ReadAllBytes(actions);

        CommandResult result = Run(checkpoint, actions, output);

        Assert.Equal(HeadlessGameCommand.SuccessExitCode, result.ExitCode);
        Assert.Contains("Policy: explicit open-grid test harness", result.Output, StringComparison.Ordinal);
        Assert.Contains("Actions applied: 3", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(directory, result.AllOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(checkpointBefore, File.ReadAllBytes(checkpoint));
        Assert.Equal(actionsBefore, File.ReadAllBytes(actions));
        GameSessionState replayed = sessionStore.Load(output).Session!;
        Assert.Equal("red", replayed.Turn.ActiveParticipantId);
        Assert.Equal(new GridPosition(1, 0), replayed.Actors["blue-actor"].Position);
        Assert.Equal(new GridPosition(3, 3), replayed.Actors["red-actor"].Position);
    }

    [Fact]
    public void ReplayOpenGrid_SameInputs_ProducesByteIdenticalCheckpoints()
    {
        string checkpoint = SaveSession();
        string actions = SaveActions(CreateActions());
        string first = Path.Combine(directory, "first.json");
        string second = Path.Combine(directory, "second.json");

        Assert.Equal(0, Run(checkpoint, actions, first).ExitCode);
        Assert.Equal(0, Run(checkpoint, actions, second).ExitCode);

        Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
    }

    [Fact]
    public void ReplayOpenGrid_RejectedAction_DoesNotCreateOutput()
    {
        string checkpoint = SaveSession();
        string actions = SaveActions(
            [GameSessionAction.MoveActor(1, "red-actor", new GridPosition(3, 3))]);
        string output = Path.Combine(directory, "rejected.json");

        CommandResult result = Run(checkpoint, actions, output);

        Assert.Equal(HeadlessGameCommand.ValidationErrorExitCode, result.ExitCode);
        Assert.Contains("rejected atomically", result.Error, StringComparison.Ordinal);
        Assert.Contains(nameof(GameSessionMovementStatus.ActorNotControlledByActiveParticipant), result.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void ReplayOpenGrid_InvalidActionLog_ReturnsValidationErrorWithoutOutput()
    {
        string checkpoint = SaveSession();
        string actions = Path.Combine(directory, "invalid-actions.json");
        File.WriteAllText(actions, "not-json");
        string output = Path.Combine(directory, "invalid-output.json");

        CommandResult result = Run(checkpoint, actions, output);

        Assert.Equal(HeadlessGameCommand.ValidationErrorExitCode, result.ExitCode);
        Assert.Contains("Code: InvalidJson", result.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void ReplayOpenGrid_MissingInputs_ReturnInputError()
    {
        string checkpoint = SaveSession();
        string output = Path.Combine(directory, "output.json");

        CommandResult missingCheckpoint = Run(
            Path.Combine(directory, "missing-session.json"),
            Path.Combine(directory, "missing-actions.json"),
            output);
        CommandResult missingActions = Run(
            checkpoint,
            Path.Combine(directory, "missing-actions.json"),
            output);

        Assert.Equal(HeadlessGameCommand.InputErrorExitCode, missingCheckpoint.ExitCode);
        Assert.Equal(HeadlessGameCommand.InputErrorExitCode, missingActions.ExitCode);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void ReplayOpenGrid_InvalidOutputDirectory_ReturnsInputError()
    {
        string checkpoint = SaveSession();
        string actions = SaveActions(CreateActions());
        string output = Path.Combine(directory, "missing", "output.json");

        CommandResult result = Run(checkpoint, actions, output);

        Assert.Equal(HeadlessGameCommand.InputErrorExitCode, result.ExitCode);
        Assert.Contains("Code: InvalidPath", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplayOpenGrid_OutputConflictingWithEitherInput_IsRejectedBeforeWriting()
    {
        string checkpoint = SaveSession();
        string actions = SaveActions(CreateActions());
        byte[] checkpointBefore = File.ReadAllBytes(checkpoint);
        byte[] actionsBefore = File.ReadAllBytes(actions);

        CommandResult checkpointConflict = Run(checkpoint, actions, checkpoint);
        CommandResult actionConflict = Run(checkpoint, actions, actions);

        Assert.Equal(HeadlessGameCommand.InputErrorExitCode, checkpointConflict.ExitCode);
        Assert.Equal(HeadlessGameCommand.InputErrorExitCode, actionConflict.ExitCode);
        Assert.Contains("OutputConflictsWithInput", checkpointConflict.Error, StringComparison.Ordinal);
        Assert.Contains("OutputConflictsWithInput", actionConflict.Error, StringComparison.Ordinal);
        Assert.Equal(checkpointBefore, File.ReadAllBytes(checkpoint));
        Assert.Equal(actionsBefore, File.ReadAllBytes(actions));
    }

    [Theory]
    [InlineData()]
    [InlineData("replay-open-grid")]
    [InlineData("replay-open-grid", "session", "actions", "--bad", "output")]
    [InlineData("unknown", "session", "actions", "--output", "output")]
    public void InvalidArguments_ReturnUsageError(params string[] arguments)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = HeadlessReplayCommand.Run(
            arguments,
            sessionStore,
            new GameSessionActionLogFileStore(actionSerializer),
            processor,
            output,
            error);

        Assert.Equal(HeadlessGameCommand.UsageErrorExitCode, exitCode);
        Assert.Contains("Usage:", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void UnexpectedFailure_ReturnsSoftwareErrorWithoutStackTrace()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = HeadlessReplayCommand.Run(
            ["replay-open-grid", "session", "actions", "--output", "output"],
            new ThrowingSessionStore(),
            new GameSessionActionLogFileStore(actionSerializer),
            processor,
            output,
            error);

        Assert.Equal(HeadlessGameCommand.SoftwareErrorExitCode, exitCode);
        Assert.Contains("Code: UnexpectedError", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("System.", error.ToString(), StringComparison.Ordinal);
    }

    public void Dispose()
    {
        Directory.Delete(directory, true);
        GC.SuppressFinalize(this);
    }

    private string SaveSession()
    {
        string path = Path.Combine(directory, "input-session.json");
        GameSessionState session = GameSessionState.Create(
            new GridSize(5, 5),
            ["blue", "red"],
            [
                new GameActorDefinition("blue-actor", "blue", new GridPosition(0, 0), 3),
                new GameActorDefinition("red-actor", "red", new GridPosition(4, 3), 2),
            ]).Session!;
        Assert.True(sessionStore.Save(path, session).IsSuccess);
        return path;
    }

    private string SaveActions(IReadOnlyList<GameSessionAction> actions)
    {
        string path = Path.Combine(directory, "actions.json");
        GameSessionActionLogSerializationResult serialization = actionSerializer.Serialize(
            new GameSessionActionLog(GameSessionActionLogFormatV1.Version, actions));
        Assert.True(serialization.IsSuccess);
        File.WriteAllBytes(path, serialization.Data!);
        return path;
    }

    private CommandResult Run(string checkpoint, string actions, string outputPath)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        int exitCode = HeadlessReplayCommand.Run(
            ["replay-open-grid", checkpoint, actions, "--output", outputPath],
            sessionStore,
            new GameSessionActionLogFileStore(actionSerializer),
            processor,
            output,
            error);
        return new CommandResult(exitCode, output.ToString(), error.ToString());
    }

    private static IReadOnlyList<GameSessionAction> CreateActions() =>
    [
        GameSessionAction.MoveActor(1, "blue-actor", new GridPosition(1, 0)),
        GameSessionAction.AdvanceTurn(2),
        GameSessionAction.MoveActor(3, "red-actor", new GridPosition(3, 3)),
    ];

    private sealed record CommandResult(int ExitCode, string Output, string Error)
    {
        public string AllOutput => Output + Error;
    }

    private sealed class ThrowingSessionStore : IGameSessionFileStore
    {
        public GameSessionLoadResult Load(string path) => throw new IOException("Synthetic failure.");

        public GameSessionSaveResult Save(string path, GameSessionState? session) =>
            throw new IOException("Synthetic failure.");
    }
}
