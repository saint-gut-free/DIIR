using DisciplesRemaster.Core.Geometry;

namespace DisciplesRemaster.Core.Navigation;

public enum GridPathSearchStatus
{
    Success,
    StartOutsideGrid,
    GoalOutsideGrid,
    StartBlocked,
    GoalBlocked,
    NoPath,
    SearchLimitExceeded,
}

public sealed record GridPathSearchResult(
    GridPathSearchStatus Status,
    IReadOnlyList<GridPosition> Path,
    int VisitedPositions)
{
    public bool IsSuccess => Status == GridPathSearchStatus.Success;

    public int StepCount => IsSuccess ? Math.Max(0, Path.Count - 1) : 0;
}

public interface IGridPathfinder
{
    GridPathSearchResult FindPath(
        GridSize size,
        GridPosition start,
        GridPosition goal,
        IGridTopology topology,
        Func<GridPosition, bool> canEnter,
        int maximumVisitedPositions = GridPathfinder.DefaultMaximumVisitedPositions);
}
