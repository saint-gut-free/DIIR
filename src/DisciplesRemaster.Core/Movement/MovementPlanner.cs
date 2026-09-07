using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Core.Navigation;

namespace DisciplesRemaster.Core.Movement;

public enum MovementPlanStatus
{
    Success,
    InvalidMovementBudget,
    StartOutsideGrid,
    DestinationOutsideGrid,
    StartBlocked,
    DestinationBlocked,
    NoPath,
    SearchLimitExceeded,
    MovementBudgetExceeded,
}

public sealed record MovementPlan(
    MovementPlanStatus Status,
    IReadOnlyList<GridPosition> Path,
    int RequiredSteps,
    int MovementBudget,
    int VisitedPositions)
{
    public bool IsSuccess => Status == MovementPlanStatus.Success;

    public int RemainingMovement => IsSuccess ? MovementBudget - RequiredSteps : 0;
}

public interface IMovementPlanner
{
    MovementPlan Plan(
        GridSize size,
        GridPosition start,
        GridPosition destination,
        int movementBudget,
        IGridTopology topology,
        Func<GridPosition, bool> canEnter,
        int maximumVisitedPositions = GridPathfinder.DefaultMaximumVisitedPositions);
}

/// <summary>
/// Applies an explicit project-owned step budget to an engine-neutral path search.
/// One traversed topology edge costs one movement point in this v1 rule.
/// </summary>
public sealed class MovementPlanner : IMovementPlanner
{
    private readonly IGridPathfinder pathfinder;

    public MovementPlanner(IGridPathfinder pathfinder)
    {
        this.pathfinder = pathfinder ?? throw new ArgumentNullException(nameof(pathfinder));
    }

    public MovementPlan Plan(
        GridSize size,
        GridPosition start,
        GridPosition destination,
        int movementBudget,
        IGridTopology topology,
        Func<GridPosition, bool> canEnter,
        int maximumVisitedPositions = GridPathfinder.DefaultMaximumVisitedPositions)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(canEnter);

        if (movementBudget < 0)
        {
            return Failure(MovementPlanStatus.InvalidMovementBudget, movementBudget);
        }

        GridPathSearchResult search = pathfinder.FindPath(
            size,
            start,
            destination,
            topology,
            canEnter,
            maximumVisitedPositions);
        if (!search.IsSuccess)
        {
            return new MovementPlan(
                MapStatus(search.Status),
                [],
                0,
                movementBudget,
                search.VisitedPositions);
        }

        if (search.StepCount > movementBudget)
        {
            return new MovementPlan(
                MovementPlanStatus.MovementBudgetExceeded,
                search.Path,
                search.StepCount,
                movementBudget,
                search.VisitedPositions);
        }

        return new MovementPlan(
            MovementPlanStatus.Success,
            search.Path,
            search.StepCount,
            movementBudget,
            search.VisitedPositions);
    }

    private static MovementPlanStatus MapStatus(GridPathSearchStatus status) =>
        status switch
        {
            GridPathSearchStatus.StartOutsideGrid => MovementPlanStatus.StartOutsideGrid,
            GridPathSearchStatus.GoalOutsideGrid => MovementPlanStatus.DestinationOutsideGrid,
            GridPathSearchStatus.StartBlocked => MovementPlanStatus.StartBlocked,
            GridPathSearchStatus.GoalBlocked => MovementPlanStatus.DestinationBlocked,
            GridPathSearchStatus.NoPath => MovementPlanStatus.NoPath,
            GridPathSearchStatus.SearchLimitExceeded => MovementPlanStatus.SearchLimitExceeded,
            _ => throw new InvalidOperationException("Successful path status must be handled before mapping."),
        };

    private static MovementPlan Failure(MovementPlanStatus status, int movementBudget) =>
        new(status, [], 0, movementBudget, 0);
}
