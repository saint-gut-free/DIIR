using DisciplesRemaster.Core.Geometry;

namespace DisciplesRemaster.Core.Navigation;

/// <summary>
/// Deterministic unweighted breadth-first path search. Topology and passability
/// are explicit inputs and therefore remain outside the algorithm.
/// </summary>
public sealed class GridPathfinder : IGridPathfinder
{
    public const int DefaultMaximumVisitedPositions = 1_000_000;

    public GridPathSearchResult FindPath(
        GridSize size,
        GridPosition start,
        GridPosition goal,
        IGridTopology topology,
        Func<GridPosition, bool> canEnter,
        int maximumVisitedPositions = DefaultMaximumVisitedPositions)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(canEnter);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumVisitedPositions);

        if (!size.Contains(start))
        {
            return Failure(GridPathSearchStatus.StartOutsideGrid);
        }

        if (!size.Contains(goal))
        {
            return Failure(GridPathSearchStatus.GoalOutsideGrid);
        }

        if (!canEnter(start))
        {
            return Failure(GridPathSearchStatus.StartBlocked);
        }

        if (!canEnter(goal))
        {
            return Failure(GridPathSearchStatus.GoalBlocked);
        }

        var frontier = new Queue<GridPosition>();
        var previous = new Dictionary<GridPosition, GridPosition?>
        {
            [start] = null,
        };
        frontier.Enqueue(start);

        while (frontier.Count > 0)
        {
            GridPosition current = frontier.Dequeue();
            if (current == goal)
            {
                return Success(Reconstruct(previous, current), previous.Count);
            }

            foreach (GridPosition neighbor in topology.GetNeighbors(current))
            {
                if (!size.Contains(neighbor) || previous.ContainsKey(neighbor) || !canEnter(neighbor))
                {
                    continue;
                }

                if (previous.Count >= maximumVisitedPositions)
                {
                    return new GridPathSearchResult(
                        GridPathSearchStatus.SearchLimitExceeded,
                        [],
                        previous.Count);
                }

                previous.Add(neighbor, current);
                frontier.Enqueue(neighbor);
            }
        }

        return new GridPathSearchResult(GridPathSearchStatus.NoPath, [], previous.Count);
    }

    private static IReadOnlyList<GridPosition> Reconstruct(
        IReadOnlyDictionary<GridPosition, GridPosition?> previous,
        GridPosition goal)
    {
        List<GridPosition> path = [];
        GridPosition? current = goal;
        while (current is not null)
        {
            path.Add(current.Value);
            current = previous[current.Value];
        }

        path.Reverse();
        return path.AsReadOnly();
    }

    private static GridPathSearchResult Success(IReadOnlyList<GridPosition> path, int visited) =>
        new(GridPathSearchStatus.Success, path, visited);

    private static GridPathSearchResult Failure(GridPathSearchStatus status) =>
        new(status, [], 0);
}
