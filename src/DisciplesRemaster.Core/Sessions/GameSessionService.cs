using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Core.Movement;
using DisciplesRemaster.Core.Navigation;
using DisciplesRemaster.Core.Turns;

namespace DisciplesRemaster.Core.Sessions;

public sealed class GameSessionService
{
    private readonly IMovementPlanner movementPlanner;

    public GameSessionService(IMovementPlanner movementPlanner)
    {
        this.movementPlanner = movementPlanner ?? throw new ArgumentNullException(nameof(movementPlanner));
    }

    public GameSessionMovementResult MoveActor(
        GameSessionState session,
        string actorId,
        GridPosition destination,
        IGridTopology topology,
        Func<GridPosition, bool> canEnter,
        int maximumVisitedPositions = GridPathfinder.DefaultMaximumVisitedPositions)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(canEnter);

        if (string.IsNullOrWhiteSpace(actorId) || !session.Actors.TryGetValue(actorId, out GameActorState? actor))
        {
            return Failure(GameSessionMovementStatus.ActorNotFound, "Actor was not found.");
        }

        if (!string.Equals(actor.OwnerParticipantId, session.Turn.ActiveParticipantId, StringComparison.Ordinal))
        {
            return Failure(
                GameSessionMovementStatus.ActorNotControlledByActiveParticipant,
                "Actor is not controlled by the active participant.");
        }

        MovementPlan plan = movementPlanner.Plan(
            session.MapSize,
            actor.Position,
            destination,
            actor.RemainingMovement,
            topology,
            canEnter,
            maximumVisitedPositions);
        if (!plan.IsSuccess)
        {
            return new GameSessionMovementResult(
                GameSessionMovementStatus.MovementPlanRejected,
                null,
                plan,
                "Movement plan was rejected.");
        }

        GameActorState movedActor = actor with
        {
            Position = destination,
            RemainingMovement = plan.RemainingMovement,
        };
        GameActorState[] actors = session.Actors.Values
            .Select(candidate => string.Equals(candidate.Id, actor.Id, StringComparison.Ordinal) ? movedActor : candidate)
            .ToArray();
        return new GameSessionMovementResult(
            GameSessionMovementStatus.Success,
            session.WithActors(actors),
            plan,
            null);
    }

    public GameSessionState AdvanceTurn(GameSessionState session)
    {
        ArgumentNullException.ThrowIfNull(session);

        RoundTurnSequence nextTurn = session.Turn.Advance();
        GameActorState[] actors = session.Actors.Values
            .Select(actor => string.Equals(actor.OwnerParticipantId, nextTurn.ActiveParticipantId, StringComparison.Ordinal)
                ? actor with { RemainingMovement = actor.MovementAllowance }
                : actor)
            .ToArray();
        return session.WithTurnAndActors(nextTurn, actors);
    }

    private static GameSessionMovementResult Failure(GameSessionMovementStatus status, string message) =>
        new(status, null, null, message);
}
