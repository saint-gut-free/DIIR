using System.Globalization;
using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Core.Navigation;
using DisciplesRemaster.Core.Sessions;
using DisciplesRemaster.Persistence.Sessions;

namespace DisciplesRemaster.Godot;

/// <summary>
/// A temporary runnable host for the engine-neutral runtime. It keeps the
/// vertical slice executable before a compatible Godot .NET host is available.
/// </summary>
public static class HeadlessGameCommand
{
    public const int SuccessExitCode = 0;
    public const int InputErrorExitCode = 2;
    public const int ValidationErrorExitCode = 3;
    public const int UsageErrorExitCode = 64;
    public const int SoftwareErrorExitCode = 70;

    public static int Run(
        IReadOnlyList<string> arguments,
        IGameSessionFileStore store,
        GameSessionService sessionService,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            return arguments.FirstOrDefault() switch
            {
                "validate-session" => Validate(arguments, store, output, error),
                "summary-session" => Summary(arguments, store, output, error),
                "advance-turn" => AdvanceTurn(arguments, store, sessionService, output, error),
                "move-open-grid" => MoveOpenGrid(arguments, store, sessionService, output, error),
                _ => UsageFailure(error, "Unknown or missing command."),
            };
        }
        catch (Exception)
        {
            error.WriteLine("Headless game host failed unexpectedly.");
            error.WriteLine("Code: UnexpectedError");
            return SoftwareErrorExitCode;
        }
    }

    private static int Validate(
        IReadOnlyList<string> arguments,
        IGameSessionFileStore store,
        TextWriter output,
        TextWriter error)
    {
        if (arguments.Count != 2 || string.IsNullOrWhiteSpace(arguments[1]))
        {
            return UsageFailure(error, "validate-session requires one checkpoint file.");
        }

        GameSessionLoadResult load = store.Load(arguments[1]);
        if (!load.IsSuccess)
        {
            return WriteLoadFailure(load, arguments[1], error);
        }

        output.WriteLine("Game session checkpoint is valid.");
        output.WriteLine($"File: {SafeName(arguments[1])}");
        return SuccessExitCode;
    }

    private static int Summary(
        IReadOnlyList<string> arguments,
        IGameSessionFileStore store,
        TextWriter output,
        TextWriter error)
    {
        if (arguments.Count != 2 || string.IsNullOrWhiteSpace(arguments[1]))
        {
            return UsageFailure(error, "summary-session requires one checkpoint file.");
        }

        GameSessionLoadResult load = store.Load(arguments[1]);
        if (!load.IsSuccess || load.Session is null)
        {
            return WriteLoadFailure(load, arguments[1], error);
        }

        GameSessionState session = load.Session;
        output.WriteLine("Headless game session summary");
        output.WriteLine($"File: {SafeName(arguments[1])}");
        output.WriteLine($"Map: {session.MapSize.Width} x {session.MapSize.Height}");
        output.WriteLine($"Round: {session.Turn.RoundNumber.ToString(CultureInfo.InvariantCulture)}");
        output.WriteLine($"Active participant: {session.Turn.ActiveParticipantId}");
        output.WriteLine($"Participants: {session.Turn.ParticipantIds.Count.ToString(CultureInfo.InvariantCulture)}");
        output.WriteLine($"Actors: {session.Actors.Count.ToString(CultureInfo.InvariantCulture)}");
        return SuccessExitCode;
    }

    private static int AdvanceTurn(
        IReadOnlyList<string> arguments,
        IGameSessionFileStore store,
        GameSessionService sessionService,
        TextWriter output,
        TextWriter error)
    {
        if (!TryParseInputAndOutput(arguments, 2, out string? inputPath, out string? outputPath))
        {
            return UsageFailure(error, "advance-turn requires <checkpoint> [--output <file>].");
        }

        GameSessionLoadResult load = store.Load(inputPath!);
        if (!load.IsSuccess || load.Session is null)
        {
            return WriteLoadFailure(load, inputPath!, error);
        }

        GameSessionState updated = sessionService.AdvanceTurn(load.Session);
        return SaveUpdated(
            store,
            updated,
            outputPath ?? inputPath!,
            "Turn advanced.",
            output,
            error);
    }

    private static int MoveOpenGrid(
        IReadOnlyList<string> arguments,
        IGameSessionFileStore store,
        GameSessionService sessionService,
        TextWriter output,
        TextWriter error)
    {
        if (!TryParseInputAndOutput(arguments, 5, out string? inputPath, out string? outputPath) ||
            string.IsNullOrWhiteSpace(arguments[2]) ||
            !int.TryParse(arguments[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) ||
            !int.TryParse(arguments[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
        {
            return UsageFailure(error, "move-open-grid requires <checkpoint> <actor-id> <x> <y> [--output <file>].");
        }

        GameSessionLoadResult load = store.Load(inputPath!);
        if (!load.IsSuccess || load.Session is null)
        {
            return WriteLoadFailure(load, inputPath!, error);
        }

        GameSessionMovementResult movement = sessionService.MoveActor(
            load.Session,
            arguments[2],
            new GridPosition(x, y),
            OrthogonalGridTopology.Instance,
            _ => true);
        if (!movement.IsSuccess || movement.Session is null)
        {
            error.WriteLine("Actor movement was rejected.");
            error.WriteLine($"Code: {movement.Status}");
            if (movement.Plan is not null)
            {
                error.WriteLine($"Plan: {movement.Plan.Status}");
                error.WriteLine($"Required steps: {movement.Plan.RequiredSteps.ToString(CultureInfo.InvariantCulture)}");
                error.WriteLine($"Available movement: {movement.Plan.MovementBudget.ToString(CultureInfo.InvariantCulture)}");
            }

            return ValidationErrorExitCode;
        }

        return SaveUpdated(
            store,
            movement.Session,
            outputPath ?? inputPath!,
            "Actor moved on the explicit open-grid harness.",
            output,
            error);
    }

    private static int SaveUpdated(
        IGameSessionFileStore store,
        GameSessionState session,
        string path,
        string successMessage,
        TextWriter output,
        TextWriter error)
    {
        GameSessionSaveResult save = store.Save(path, session);
        if (!save.IsSuccess)
        {
            error.WriteLine("Game session checkpoint could not be saved.");
            error.WriteLine($"Code: {save.ErrorCode}");
            error.WriteLine($"File: {SafeName(path)}");
            WriteIssues(save.Issues, error);
            return ExitCodeFor(save.ErrorCode);
        }

        output.WriteLine(successMessage);
        output.WriteLine($"File: {SafeName(path)}");
        output.WriteLine($"Round: {session.Turn.RoundNumber.ToString(CultureInfo.InvariantCulture)}");
        output.WriteLine($"Active participant: {session.Turn.ActiveParticipantId}");
        return SuccessExitCode;
    }

    private static int WriteLoadFailure(GameSessionLoadResult load, string path, TextWriter error)
    {
        error.WriteLine("Game session checkpoint could not be loaded.");
        error.WriteLine($"Code: {load.ErrorCode}");
        error.WriteLine($"File: {SafeName(path)}");
        WriteIssues(load.Issues, error);
        return ExitCodeFor(load.ErrorCode);
    }

    private static void WriteIssues(IEnumerable<GameSessionPersistenceIssue> issues, TextWriter writer)
    {
        foreach (GameSessionPersistenceIssue issue in issues)
        {
            writer.WriteLine($"{issue.Code}/{issue.DetailCode} [{issue.PropertyPath}]: {issue.Message}");
        }
    }

    private static int ExitCodeFor(GameSessionPersistenceErrorCode code) =>
        code switch
        {
            GameSessionPersistenceErrorCode.InvalidJson or
            GameSessionPersistenceErrorCode.ValidationFailed => ValidationErrorExitCode,
            GameSessionPersistenceErrorCode.UnexpectedError => SoftwareErrorExitCode,
            _ => InputErrorExitCode,
        };

    private static bool TryParseInputAndOutput(
        IReadOnlyList<string> arguments,
        int requiredCount,
        out string? inputPath,
        out string? outputPath)
    {
        inputPath = null;
        outputPath = null;
        int count = arguments.Count;
        if (count != requiredCount && count != requiredCount + 2)
        {
            return false;
        }

        inputPath = arguments[1];
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return false;
        }

        if (arguments.Count == requiredCount)
        {
            return true;
        }

        if (!string.Equals(arguments[requiredCount], "--output", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(arguments[requiredCount + 1]))
        {
            return false;
        }

        outputPath = arguments[requiredCount + 1];
        return true;
    }

    private static int UsageFailure(TextWriter error, string message)
    {
        error.WriteLine(message);
        WriteUsage(error);
        return UsageErrorExitCode;
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage:");
        writer.WriteLine("  validate-session <checkpoint>");
        writer.WriteLine("  summary-session <checkpoint>");
        writer.WriteLine("  advance-turn <checkpoint> [--output <file>]");
        writer.WriteLine("  move-open-grid <checkpoint> <actor-id> <x> <y> [--output <file>]");
    }

    private static string SafeName(string path)
    {
        string name = Path.GetFileName(path.Trim());
        return string.IsNullOrWhiteSpace(name) ? "<session>" : name;
    }
}
