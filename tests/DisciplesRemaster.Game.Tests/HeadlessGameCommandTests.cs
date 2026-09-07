using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Core.Movement;
using DisciplesRemaster.Core.Navigation;
using DisciplesRemaster.Core.Sessions;
using DisciplesRemaster.Godot;
using DisciplesRemaster.Persistence.Sessions;

namespace DisciplesRemaster.Game.Tests;

public sealed class HeadlessGameCommandTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"d2r-headless-{Guid.NewGuid():N}");
    private readonly GameSessionFileStore store = new(new GameSessionJsonSerializer());
    private readonly GameSessionService service = new(new MovementPlanner(new GridPathfinder()));

    public HeadlessGameCommandTests()
    {
        Directory.CreateDirectory(directory);
    }

    [Fact]
    public void ValidateAndSummary_ValidCheckpoint_ReturnSuccessWithoutAbsolutePath()
    {
        string path = Save("private-session.json", CreateSession());

        CommandResult validation = Run("validate-session", path);
        CommandResult summary = Run("summary-session", path);

        Assert.Equal(0, validation.ExitCode);
        Assert.Equal(0, summary.ExitCode);
        Assert.Contains("private-session.json", validation.Output, StringComparison.Ordinal);
        Assert.Contains("Active participant: blue", summary.Output, StringComparison.Ordinal);
        Assert.Contains("Actors: 2", summary.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(directory, validation.AllOutput, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(directory, summary.AllOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AdvanceTurn_WithOutput_WritesNewCheckpointAndPreservesInput()
    {
        string input = Save("input.json", CreateSession());
        string output = Path.Combine(directory, "output.json");
        byte[] original = File.ReadAllBytes(input);

        CommandResult result = Run("advance-turn", input, "--output", output);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(original, File.ReadAllBytes(input));
        GameSessionState updated = store.Load(output).Session!;
        Assert.Equal("red", updated.Turn.ActiveParticipantId);
    }

    [Fact]
    public void MoveOpenGrid_ActiveActor_WritesUpdatedCheckpoint()
    {
        string input = Save("input.json", CreateSession());
        string output = Path.Combine(directory, "moved.json");

        CommandResult result = Run("move-open-grid", input, "blue-actor", "2", "0", "--output", output);

        Assert.Equal(0, result.ExitCode);
        GameActorState actor = store.Load(output).Session!.Actors["blue-actor"];
        Assert.Equal(new GridPosition(2, 0), actor.Position);
        Assert.Equal(1, actor.RemainingMovement);
        Assert.Contains("explicit open-grid harness", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void MoveOpenGrid_NonActiveOrOverBudget_ReturnsValidationExitCodeWithoutOutput()
    {
        string input = Save("input.json", CreateSession());
        string nonActiveOutput = Path.Combine(directory, "non-active.json");
        string overBudgetOutput = Path.Combine(directory, "over-budget.json");

        CommandResult nonActive = Run(
            "move-open-grid", input, "red-actor", "3", "3", "--output", nonActiveOutput);
        CommandResult overBudget = Run(
            "move-open-grid", input, "blue-actor", "4", "4", "--output", overBudgetOutput);

        Assert.Equal(3, nonActive.ExitCode);
        Assert.Contains(nameof(GameSessionMovementStatus.ActorNotControlledByActiveParticipant), nonActive.Error);
        Assert.False(File.Exists(nonActiveOutput));
        Assert.Equal(3, overBudget.ExitCode);
        Assert.Contains(nameof(MovementPlanStatus.MovementBudgetExceeded), overBudget.Error);
        Assert.False(File.Exists(overBudgetOutput));
    }

    [Fact]
    public void MissingOrInvalidCheckpoint_ReturnsDocumentSpecificExitCodes()
    {
        string invalid = Path.Combine(directory, "invalid.json");
        File.WriteAllText(invalid, "not-json");

        Assert.Equal(2, Run("validate-session", Path.Combine(directory, "missing.json")).ExitCode);
        Assert.Equal(3, Run("validate-session", invalid).ExitCode);
    }

    [Theory]
    [InlineData()]
    [InlineData("unknown")]
    [InlineData("advance-turn")]
    [InlineData("move-open-grid", "file", "actor", "not-number", "0")]
    [InlineData("summary-session", "one", "two")]
    public void InvalidArguments_ReturnUsageExitCode(params string[] arguments)
    {
        Assert.Equal(64, Run(arguments).ExitCode);
    }

    [Fact]
    public void UnexpectedFailure_ReturnsSoftwareExitCodeWithoutStackTrace()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        int exitCode = HeadlessGameCommand.Run(
            ["validate-session", "session.json"],
            new ThrowingStore(),
            service,
            output,
            error);

        Assert.Equal(70, exitCode);
        Assert.Contains("UnexpectedError", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", error.ToString(), StringComparison.Ordinal);
    }

    public void Dispose()
    {
        Directory.Delete(directory, true);
        GC.SuppressFinalize(this);
    }

    private string Save(string filename, GameSessionState session)
    {
        string path = Path.Combine(directory, filename);
        Assert.True(store.Save(path, session).IsSuccess);
        return path;
    }

    private CommandResult Run(params string[] arguments)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int exitCode = HeadlessGameCommand.Run(arguments, store, service, output, error);
        return new CommandResult(exitCode, output.ToString(), error.ToString());
    }

    private static GameSessionState CreateSession() =>
        GameSessionState.Create(
            new GridSize(5, 5),
            ["blue", "red"],
            [
                new GameActorDefinition("blue-actor", "blue", new GridPosition(0, 0), 3),
                new GameActorDefinition("red-actor", "red", new GridPosition(4, 3), 2),
            ]).Session!;

    private sealed record CommandResult(int ExitCode, string Output, string Error)
    {
        public string AllOutput => Output + Error;
    }

    private sealed class ThrowingStore : IGameSessionFileStore
    {
        public GameSessionLoadResult Load(string path) => throw new InvalidOperationException("synthetic");

        public GameSessionSaveResult Save(string path, GameSessionState? session) =>
            throw new InvalidOperationException("synthetic");
    }
}
