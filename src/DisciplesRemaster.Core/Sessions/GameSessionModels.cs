using System.Collections.ObjectModel;
using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Core.Movement;
using DisciplesRemaster.Core.Turns;

namespace DisciplesRemaster.Core.Sessions;

public static class GameSessionRules
{
    public const int MaximumActors = 100_000;
    public const int MaximumActorIdLength = 96;
}

public sealed record GameActorDefinition(
    string Id,
    string OwnerParticipantId,
    GridPosition Position,
    int MovementAllowance);

public sealed record GameActorState(
    string Id,
    string OwnerParticipantId,
    GridPosition Position,
    int MovementAllowance,
    int RemainingMovement);

public enum GameSessionValidationCode
{
    TurnSequenceInvalid,
    TooManyActors,
    ActorMissing,
    ActorIdMissing,
    ActorIdTooLong,
    DuplicateActorId,
    OwnerParticipantIdMissing,
    UnknownOwnerParticipant,
    ActorOutsideGrid,
    InvalidMovementAllowance,
}

public sealed record GameSessionValidationIssue(
    GameSessionValidationCode Code,
    string PropertyPath,
    string DetailCode,
    string Message);

public sealed record GameSessionCreationResult(
    GameSessionState? Session,
    IReadOnlyList<GameSessionValidationIssue> Issues)
{
    public bool IsSuccess => Session is not null && Issues.Count == 0;
}

public enum GameSessionMovementStatus
{
    Success,
    ActorNotFound,
    ActorNotControlledByActiveParticipant,
    MovementPlanRejected,
}

public sealed record GameSessionMovementResult(
    GameSessionMovementStatus Status,
    GameSessionState? Session,
    MovementPlan? Plan,
    string? Message)
{
    public bool IsSuccess => Status == GameSessionMovementStatus.Success && Session is not null;
}

/// <summary>
/// Immutable state for the minimal project-owned turn and movement slice.
/// </summary>
public sealed class GameSessionState
{
    private readonly ReadOnlyDictionary<string, GameActorState> actors;

    internal GameSessionState(
        GridSize mapSize,
        RoundTurnSequence turn,
        IEnumerable<GameActorState> actors)
    {
        MapSize = mapSize;
        Turn = turn;
        this.actors = new ReadOnlyDictionary<string, GameActorState>(
            actors
                .OrderBy(actor => actor.Id, StringComparer.Ordinal)
                .ToDictionary(actor => actor.Id, StringComparer.Ordinal));
    }

    public GridSize MapSize { get; }

    public RoundTurnSequence Turn { get; }

    public IReadOnlyDictionary<string, GameActorState> Actors => actors;

    public static GameSessionCreationResult Create(
        GridSize mapSize,
        IEnumerable<string?> participantIds,
        IEnumerable<GameActorDefinition?> actorDefinitions)
    {
        ArgumentNullException.ThrowIfNull(participantIds);
        ArgumentNullException.ThrowIfNull(actorDefinitions);

        RoundTurnSequenceCreationResult turnResult = RoundTurnSequence.Create(participantIds);
        GameActorDefinition?[] definitions = actorDefinitions.ToArray();
        List<GameSessionValidationIssue> issues = turnResult.Issues
            .Select(issue => new GameSessionValidationIssue(
                GameSessionValidationCode.TurnSequenceInvalid,
                issue.PropertyPath,
                issue.Code.ToString(),
                issue.Message))
            .ToList();

        if (definitions.Length > GameSessionRules.MaximumActors)
        {
            issues.Add(Issue(
                GameSessionValidationCode.TooManyActors,
                "actors",
                nameof(GameSessionValidationCode.TooManyActors),
                "The game session contains too many actors."));
        }

        HashSet<string> owners = turnResult.Sequence is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : turnResult.Sequence.ParticipantIds.ToHashSet(StringComparer.Ordinal);
        HashSet<string> actorIds = new(StringComparer.Ordinal);
        List<GameActorState> actorStates = [];
        for (int index = 0; index < definitions.Length; index++)
        {
            GameActorDefinition? definition = definitions[index];
            string path = $"actors[{index}]";
            if (definition is null)
            {
                issues.Add(Issue(
                    GameSessionValidationCode.ActorMissing,
                    path,
                    nameof(GameSessionValidationCode.ActorMissing),
                    "Actor definition is missing."));
                continue;
            }

            ValidateActor(definition, path, mapSize, owners, actorIds, issues);
            actorStates.Add(new GameActorState(
                definition.Id,
                definition.OwnerParticipantId,
                definition.Position,
                definition.MovementAllowance,
                Math.Max(0, definition.MovementAllowance)));
        }

        GameSessionValidationIssue[] sortedIssues = issues
            .OrderBy(issue => issue.PropertyPath, StringComparer.Ordinal)
            .ThenBy(issue => issue.Code)
            .ThenBy(issue => issue.DetailCode, StringComparer.Ordinal)
            .ToArray();
        if (sortedIssues.Length > 0 || turnResult.Sequence is null)
        {
            return new GameSessionCreationResult(null, sortedIssues);
        }

        return new GameSessionCreationResult(
            new GameSessionState(mapSize, turnResult.Sequence, actorStates),
            []);
    }

    internal GameSessionState WithActors(IEnumerable<GameActorState> updatedActors) =>
        new(MapSize, Turn, updatedActors);

    internal GameSessionState WithTurnAndActors(
        RoundTurnSequence updatedTurn,
        IEnumerable<GameActorState> updatedActors) =>
        new(MapSize, updatedTurn, updatedActors);

    private static void ValidateActor(
        GameActorDefinition definition,
        string path,
        GridSize mapSize,
        IReadOnlySet<string> owners,
        ISet<string> actorIds,
        ICollection<GameSessionValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            issues.Add(Issue(GameSessionValidationCode.ActorIdMissing, $"{path}.id", nameof(GameSessionValidationCode.ActorIdMissing), "Actor ID is required."));
        }
        else
        {
            if (definition.Id.Length > GameSessionRules.MaximumActorIdLength)
            {
                issues.Add(Issue(GameSessionValidationCode.ActorIdTooLong, $"{path}.id", nameof(GameSessionValidationCode.ActorIdTooLong), "Actor ID is too long."));
            }

            if (!actorIds.Add(definition.Id))
            {
                issues.Add(Issue(GameSessionValidationCode.DuplicateActorId, $"{path}.id", nameof(GameSessionValidationCode.DuplicateActorId), "Actor IDs must be unique."));
            }
        }

        if (string.IsNullOrWhiteSpace(definition.OwnerParticipantId))
        {
            issues.Add(Issue(GameSessionValidationCode.OwnerParticipantIdMissing, $"{path}.ownerParticipantId", nameof(GameSessionValidationCode.OwnerParticipantIdMissing), "Actor owner participant ID is required."));
        }
        else if (!owners.Contains(definition.OwnerParticipantId))
        {
            issues.Add(Issue(GameSessionValidationCode.UnknownOwnerParticipant, $"{path}.ownerParticipantId", nameof(GameSessionValidationCode.UnknownOwnerParticipant), "Actor owner is not a session participant."));
        }

        if (!mapSize.Contains(definition.Position))
        {
            issues.Add(Issue(GameSessionValidationCode.ActorOutsideGrid, $"{path}.position", nameof(GameSessionValidationCode.ActorOutsideGrid), "Actor position is outside the session grid."));
        }

        if (definition.MovementAllowance < 0)
        {
            issues.Add(Issue(GameSessionValidationCode.InvalidMovementAllowance, $"{path}.movementAllowance", nameof(GameSessionValidationCode.InvalidMovementAllowance), "Movement allowance cannot be negative."));
        }
    }

    private static GameSessionValidationIssue Issue(
        GameSessionValidationCode code,
        string propertyPath,
        string detailCode,
        string message) =>
        new(code, propertyPath, detailCode, message);
}
