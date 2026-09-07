using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Core.Navigation;

namespace DisciplesRemaster.Core.Sessions;

public static class GameSessionActionRules
{
    public const int MaximumActionsPerBatch = 10_000;
}

public enum GameSessionActionKind
{
    AdvanceTurn,
    MoveActor,
}

/// <summary>
/// A project-owned command for the minimal immutable runtime slice. It does not
/// describe or claim compatibility with original-game actions.
/// </summary>
public sealed record GameSessionAction(
    int Sequence,
    GameSessionActionKind Kind,
    string? ActorId,
    GridPosition? Destination)
{
    public static GameSessionAction AdvanceTurn(int sequence) =>
        new(sequence, GameSessionActionKind.AdvanceTurn, null, null);

    public static GameSessionAction MoveActor(int sequence, string actorId, GridPosition destination) =>
        new(sequence, GameSessionActionKind.MoveActor, actorId, destination);
}

public enum GameSessionActionIssueCode
{
    TooManyActions,
    ActionMissing,
    InvalidSequence,
    UnexpectedActorId,
    UnexpectedDestination,
    ActorIdMissing,
    ActorIdTooLong,
    DestinationMissing,
    ActionRejected,
}

public sealed record GameSessionActionIssue(
    GameSessionActionIssueCode Code,
    string PropertyPath,
    string DetailCode,
    string Message);

public sealed record GameSessionActionBatchResult(
    GameSessionState? Session,
    int AppliedActions,
    IReadOnlyList<GameSessionActionIssue> Issues)
{
    public bool IsSuccess => Session is not null && Issues.Count == 0;
}

public static class GameSessionActionValidation
{
    public static IReadOnlyList<GameSessionActionIssue> Validate(
        IReadOnlyList<GameSessionAction?> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        List<GameSessionActionIssue> issues = [];
        if (actions.Count > GameSessionActionRules.MaximumActionsPerBatch)
        {
            issues.Add(Issue(
                GameSessionActionIssueCode.TooManyActions,
                "actions",
                nameof(GameSessionActionIssueCode.TooManyActions),
                "The action batch exceeds the safety limit."));
        }

        for (int index = 0; index < actions.Count; index++)
        {
            GameSessionAction? action = actions[index];
            string path = $"actions[{index}]";
            if (action is null)
            {
                issues.Add(Issue(
                    GameSessionActionIssueCode.ActionMissing,
                    path,
                    nameof(GameSessionActionIssueCode.ActionMissing),
                    "Action is missing."));
                continue;
            }

            if (action.Sequence != index + 1)
            {
                issues.Add(Issue(
                    GameSessionActionIssueCode.InvalidSequence,
                    $"{path}.sequence",
                    nameof(GameSessionActionIssueCode.InvalidSequence),
                    "Action sequence must be contiguous and start at one."));
            }

            ValidateShape(action, path, issues);
        }

        return issues
            .OrderBy(issue => issue.PropertyPath, StringComparer.Ordinal)
            .ThenBy(issue => issue.Code)
            .ThenBy(issue => issue.DetailCode, StringComparer.Ordinal)
            .ToArray();
    }

    private static void ValidateShape(
        GameSessionAction action,
        string path,
        ICollection<GameSessionActionIssue> issues)
    {
        switch (action.Kind)
        {
            case GameSessionActionKind.AdvanceTurn:
                if (action.ActorId is not null)
                {
                    issues.Add(Issue(
                        GameSessionActionIssueCode.UnexpectedActorId,
                        $"{path}.actorId",
                        nameof(GameSessionActionIssueCode.UnexpectedActorId),
                        "Advance-turn actions cannot contain an actor ID."));
                }

                if (action.Destination is not null)
                {
                    issues.Add(Issue(
                        GameSessionActionIssueCode.UnexpectedDestination,
                        $"{path}.destination",
                        nameof(GameSessionActionIssueCode.UnexpectedDestination),
                        "Advance-turn actions cannot contain a destination."));
                }

                break;
            case GameSessionActionKind.MoveActor:
                if (string.IsNullOrWhiteSpace(action.ActorId))
                {
                    issues.Add(Issue(
                        GameSessionActionIssueCode.ActorIdMissing,
                        $"{path}.actorId",
                        nameof(GameSessionActionIssueCode.ActorIdMissing),
                        "Move-actor actions require an actor ID."));
                }
                else if (action.ActorId.Length > GameSessionRules.MaximumActorIdLength)
                {
                    issues.Add(Issue(
                        GameSessionActionIssueCode.ActorIdTooLong,
                        $"{path}.actorId",
                        nameof(GameSessionActionIssueCode.ActorIdTooLong),
                        "Move-actor action ID exceeds the session actor ID limit."));
                }

                if (action.Destination is null)
                {
                    issues.Add(Issue(
                        GameSessionActionIssueCode.DestinationMissing,
                        $"{path}.destination",
                        nameof(GameSessionActionIssueCode.DestinationMissing),
                        "Move-actor actions require a destination."));
                }

                break;
            default:
                issues.Add(Issue(
                    GameSessionActionIssueCode.ActionRejected,
                    $"{path}.kind",
                    "UnsupportedActionKind",
                    "Action kind is unsupported."));
                break;
        }
    }

    private static GameSessionActionIssue Issue(
        GameSessionActionIssueCode code,
        string propertyPath,
        string detailCode,
        string message) =>
        new(code, propertyPath, detailCode, message);
}

public sealed class GameSessionActionProcessor
{
    private readonly GameSessionService sessionService;

    public GameSessionActionProcessor(GameSessionService sessionService)
    {
        this.sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
    }

    public GameSessionActionBatchResult Apply(
        GameSessionState session,
        IEnumerable<GameSessionAction?> actions,
        IGridTopology topology,
        Func<GridPosition, bool> canEnter,
        int maximumVisitedPositions = GridPathfinder.DefaultMaximumVisitedPositions)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(canEnter);

        GameSessionAction?[] actionArray = actions.ToArray();
        IReadOnlyList<GameSessionActionIssue> validationIssues = GameSessionActionValidation.Validate(actionArray);
        if (validationIssues.Count > 0)
        {
            return new GameSessionActionBatchResult(null, 0, validationIssues);
        }

        GameSessionState current = session;
        for (int index = 0; index < actionArray.Length; index++)
        {
            GameSessionAction action = actionArray[index]!;
            switch (action.Kind)
            {
                case GameSessionActionKind.AdvanceTurn:
                    current = sessionService.AdvanceTurn(current);
                    break;
                case GameSessionActionKind.MoveActor:
                    GameSessionMovementResult movement = sessionService.MoveActor(
                        current,
                        action.ActorId!,
                        action.Destination!.Value,
                        topology,
                        canEnter,
                        maximumVisitedPositions);
                    if (!movement.IsSuccess || movement.Session is null)
                    {
                        return Rejected(index, movement, index);
                    }

                    current = movement.Session;
                    break;
                default:
                    throw new InvalidOperationException("Validated action kind is unsupported.");
            }
        }

        return new GameSessionActionBatchResult(current, actionArray.Length, []);
    }

    private static GameSessionActionBatchResult Rejected(
        int actionIndex,
        GameSessionMovementResult movement,
        int appliedActions) =>
        new(
            null,
            appliedActions,
            [Issue(
                GameSessionActionIssueCode.ActionRejected,
                $"actions[{actionIndex}]",
                movement.Plan?.Status.ToString() ?? movement.Status.ToString(),
                movement.Message ?? "Action was rejected.")]);

    private static GameSessionActionIssue Issue(
        GameSessionActionIssueCode code,
        string propertyPath,
        string detailCode,
        string message) =>
        new(code, propertyPath, detailCode, message);
}
