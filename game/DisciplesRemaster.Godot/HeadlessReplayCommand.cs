using System.Globalization;
using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Core.Sessions;
using DisciplesRemaster.Persistence.Sessions;

namespace DisciplesRemaster.Godot;

public static class HeadlessReplayCommand
{
    public static int Run(
        IReadOnlyList<string> arguments,
        IGameSessionFileStore sessionStore,
        IGameSessionActionLogFileStore actionLogStore,
        GameSessionActionProcessor actionProcessor,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(sessionStore);
        ArgumentNullException.ThrowIfNull(actionLogStore);
        ArgumentNullException.ThrowIfNull(actionProcessor);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            if (arguments.Count != 5 ||
                arguments[0] != "replay-open-grid" ||
                string.IsNullOrWhiteSpace(arguments[1]) ||
                string.IsNullOrWhiteSpace(arguments[2]) ||
                arguments[3] != "--output" ||
                string.IsNullOrWhiteSpace(arguments[4]))
            {
                return UsageFailure(error);
            }

            if (PathsReferToSameFile(arguments[4], arguments[1]) ||
                PathsReferToSameFile(arguments[4], arguments[2]))
            {
                error.WriteLine("Output checkpoint must not overwrite either replay input.");
                error.WriteLine("Code: OutputConflictsWithInput");
                error.WriteLine($"Output: {SafeName(arguments[4])}");
                return HeadlessGameCommand.InputErrorExitCode;
            }

            GameSessionLoadResult sessionLoad = sessionStore.Load(arguments[1]);
            if (!sessionLoad.IsSuccess || sessionLoad.Session is null)
            {
                return WriteSessionLoadFailure(sessionLoad, arguments[1], error);
            }

            GameSessionActionLogLoadResult actionLoad = actionLogStore.Load(arguments[2]);
            if (!actionLoad.IsSuccess || actionLoad.Log is null)
            {
                return WriteActionLoadFailure(actionLoad, arguments[2], error);
            }

            GameSessionActionBatchResult replay = actionProcessor.Apply(
                sessionLoad.Session,
                actionLoad.Log.Actions,
                OrthogonalGridTopology.Instance,
                _ => true);
            if (!replay.IsSuccess || replay.Session is null)
            {
                error.WriteLine("Action log replay was rejected atomically.");
                error.WriteLine("Code: ActionRejected");
                error.WriteLine($"Actions applied before rejection: {replay.AppliedActions.ToString(CultureInfo.InvariantCulture)}");
                WriteActionIssues(replay.Issues, error);
                return HeadlessGameCommand.ValidationErrorExitCode;
            }

            GameSessionSaveResult save = sessionStore.Save(arguments[4], replay.Session);
            if (!save.IsSuccess)
            {
                error.WriteLine("Replayed checkpoint could not be saved.");
                error.WriteLine($"Code: {save.ErrorCode}");
                error.WriteLine($"File: {SafeName(arguments[4])}");
                return ExitCodeFor(save.ErrorCode);
            }

            output.WriteLine("Project-owned action log replay completed.");
            output.WriteLine("Policy: explicit open-grid test harness");
            output.WriteLine($"Input checkpoint: {SafeName(arguments[1])}");
            output.WriteLine($"Action log: {SafeName(arguments[2])}");
            output.WriteLine($"Output checkpoint: {SafeName(arguments[4])}");
            output.WriteLine($"Actions applied: {replay.AppliedActions.ToString(CultureInfo.InvariantCulture)}");
            output.WriteLine($"Round: {replay.Session.Turn.RoundNumber.ToString(CultureInfo.InvariantCulture)}");
            output.WriteLine($"Active participant: {replay.Session.Turn.ActiveParticipantId}");
            return HeadlessGameCommand.SuccessExitCode;
        }
        catch (Exception)
        {
            error.WriteLine("Action log replay failed unexpectedly.");
            error.WriteLine("Code: UnexpectedError");
            return HeadlessGameCommand.SoftwareErrorExitCode;
        }
    }

    private static int WriteSessionLoadFailure(
        GameSessionLoadResult load,
        string path,
        TextWriter error)
    {
        error.WriteLine("Input checkpoint could not be loaded.");
        error.WriteLine($"Code: {load.ErrorCode}");
        error.WriteLine($"File: {SafeName(path)}");
        return ExitCodeFor(load.ErrorCode);
    }

    private static int WriteActionLoadFailure(
        GameSessionActionLogLoadResult load,
        string path,
        TextWriter error)
    {
        error.WriteLine("Action log could not be loaded.");
        error.WriteLine($"Code: {load.ErrorCode}");
        error.WriteLine($"File: {SafeName(path)}");
        foreach (GameSessionActionLogIssue issue in load.Issues)
        {
            error.WriteLine($"{issue.Code}/{issue.DetailCode} [{issue.PropertyPath}]: {issue.Message}");
        }

        return load.ErrorCode switch
        {
            GameSessionActionLogErrorCode.InvalidJson or
            GameSessionActionLogErrorCode.ValidationFailed => HeadlessGameCommand.ValidationErrorExitCode,
            GameSessionActionLogErrorCode.UnexpectedError => HeadlessGameCommand.SoftwareErrorExitCode,
            _ => HeadlessGameCommand.InputErrorExitCode,
        };
    }

    private static int ExitCodeFor(GameSessionPersistenceErrorCode code) =>
        code switch
        {
            GameSessionPersistenceErrorCode.InvalidJson or
            GameSessionPersistenceErrorCode.ValidationFailed => HeadlessGameCommand.ValidationErrorExitCode,
            GameSessionPersistenceErrorCode.UnexpectedError => HeadlessGameCommand.SoftwareErrorExitCode,
            _ => HeadlessGameCommand.InputErrorExitCode,
        };

    private static void WriteActionIssues(
        IEnumerable<GameSessionActionIssue> issues,
        TextWriter error)
    {
        foreach (GameSessionActionIssue issue in issues)
        {
            error.WriteLine($"{issue.Code}/{issue.DetailCode} [{issue.PropertyPath}]: {issue.Message}");
        }
    }

    private static int UsageFailure(TextWriter error)
    {
        error.WriteLine("Unknown replay command or invalid arguments.");
        error.WriteLine("Usage:");
        error.WriteLine("  replay-open-grid <checkpoint> <action-log> --output <checkpoint>");
        return HeadlessGameCommand.UsageErrorExitCode;
    }

    private static bool PathsReferToSameFile(string first, string second)
    {
        try
        {
            string firstFull = Path.GetFullPath(first.Trim());
            string secondFull = Path.GetFullPath(second.Trim());
            StringComparison comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return string.Equals(firstFull, secondFull, comparison);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static string SafeName(string path)
    {
        string name = Path.GetFileName(path.Trim());
        return string.IsNullOrWhiteSpace(name) ? "<document>" : name;
    }
}
