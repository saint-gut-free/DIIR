using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Core.Navigation;

namespace DisciplesRemaster.Core.Tests;

public sealed class GridPathfinderTests
{
    private readonly GridPathfinder pathfinder = new();
    private readonly GridSize size = new(5, 4);

    [Fact]
    public void FindPath_WhenStartEqualsGoal_ReturnsSinglePosition()
    {
        var start = new GridPosition(2, 2);

        GridPathSearchResult result = Find(start, start, _ => true);

        Assert.True(result.IsSuccess);
        Assert.Equal([start], result.Path);
        Assert.Equal(0, result.StepCount);
    }

    [Fact]
    public void FindPath_OnOpenGrid_ReturnsShortestDeterministicPath()
    {
        GridPathSearchResult first = Find(new GridPosition(0, 0), new GridPosition(2, 2), _ => true);
        GridPathSearchResult second = Find(new GridPosition(0, 0), new GridPosition(2, 2), _ => true);

        GridPosition[] expected =
        [
            new(0, 0),
            new(1, 0),
            new(2, 0),
            new(2, 1),
            new(2, 2),
        ];
        Assert.Equal(expected, first.Path);
        Assert.Equal(first.Path, second.Path);
        Assert.Equal(4, first.StepCount);
    }

    [Fact]
    public void FindPath_RoutesAroundBlockedPositions()
    {
        HashSet<GridPosition> blocked = [new(1, 0), new(1, 1), new(1, 2)];

        GridPathSearchResult result = Find(
            new GridPosition(0, 0),
            new GridPosition(2, 0),
            position => !blocked.Contains(position));

        Assert.True(result.IsSuccess);
        Assert.Equal(8, result.StepCount);
        Assert.DoesNotContain(result.Path, blocked.Contains);
    }

    [Fact]
    public void FindPath_WhenNoRoute_ReturnsNoPath()
    {
        HashSet<GridPosition> blocked = [new(1, 0), new(1, 1), new(1, 2), new(1, 3)];

        GridPathSearchResult result = Find(
            new GridPosition(0, 0),
            new GridPosition(2, 0),
            position => !blocked.Contains(position));

        Assert.Equal(GridPathSearchStatus.NoPath, result.Status);
        Assert.Empty(result.Path);
    }

    [Theory]
    [InlineData(-1, 0, GridPathSearchStatus.StartOutsideGrid)]
    [InlineData(5, 0, GridPathSearchStatus.StartOutsideGrid)]
    public void FindPath_WithStartOutsideGrid_ReturnsStructuredStatus(
        int x,
        int y,
        GridPathSearchStatus expected)
    {
        GridPathSearchResult result = Find(new GridPosition(x, y), new GridPosition(0, 0), _ => true);

        Assert.Equal(expected, result.Status);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(5, 0)]
    public void FindPath_WithGoalOutsideGrid_ReturnsStructuredStatus(int x, int y)
    {
        GridPathSearchResult result = Find(new GridPosition(0, 0), new GridPosition(x, y), _ => true);

        Assert.Equal(GridPathSearchStatus.GoalOutsideGrid, result.Status);
    }

    [Fact]
    public void FindPath_WithBlockedEndpoints_ReturnsStructuredStatuses()
    {
        var start = new GridPosition(0, 0);
        var goal = new GridPosition(2, 2);

        Assert.Equal(
            GridPathSearchStatus.StartBlocked,
            Find(start, goal, position => position != start).Status);
        Assert.Equal(
            GridPathSearchStatus.GoalBlocked,
            Find(start, goal, position => position != goal).Status);
    }

    [Fact]
    public void FindPath_WhenLimitIsReached_ReturnsPartialStatusWithoutPath()
    {
        GridPathSearchResult result = pathfinder.FindPath(
            size,
            new GridPosition(0, 0),
            new GridPosition(4, 3),
            OrthogonalGridTopology.Instance,
            _ => true,
            maximumVisitedPositions: 3);

        Assert.Equal(GridPathSearchStatus.SearchLimitExceeded, result.Status);
        Assert.Equal(3, result.VisitedPositions);
        Assert.Empty(result.Path);
    }

    [Fact]
    public void FindPath_UsesCallerProvidedTopology()
    {
        var topology = new DirectTopology(new GridPosition(4, 3));

        GridPathSearchResult result = pathfinder.FindPath(
            size,
            new GridPosition(0, 0),
            new GridPosition(4, 3),
            topology,
            _ => true);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.StepCount);
    }

    [Fact]
    public void FindPath_WithInvalidLimit_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => pathfinder.FindPath(
            size,
            new GridPosition(0, 0),
            new GridPosition(1, 0),
            OrthogonalGridTopology.Instance,
            _ => true,
            0));
    }

    private GridPathSearchResult Find(
        GridPosition start,
        GridPosition goal,
        Func<GridPosition, bool> canEnter) =>
        pathfinder.FindPath(size, start, goal, OrthogonalGridTopology.Instance, canEnter);

    private sealed class DirectTopology(GridPosition destination) : IGridTopology
    {
        public IEnumerable<GridPosition> GetNeighbors(GridPosition position)
        {
            yield return destination;
        }
    }
}
