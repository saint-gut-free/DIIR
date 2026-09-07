using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Core.Movement;
using DisciplesRemaster.Core.Navigation;

namespace DisciplesRemaster.Core.Tests;

public sealed class MovementPlannerTests
{
    private readonly MovementPlanner planner = new(new GridPathfinder());
    private readonly GridSize size = new(5, 5);

    [Fact]
    public void Plan_PathWithinBudget_ReturnsRouteAndRemainingMovement()
    {
        MovementPlan result = Plan(new GridPosition(0, 0), new GridPosition(2, 1), 4, _ => true);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.RequiredSteps);
        Assert.Equal(1, result.RemainingMovement);
        Assert.Equal(new GridPosition(0, 0), result.Path[0]);
        Assert.Equal(new GridPosition(2, 1), result.Path[^1]);
    }

    [Fact]
    public void Plan_PathBeyondBudget_ReturnsRequiredBoundedRouteWithoutApplyingMovement()
    {
        MovementPlan result = Plan(new GridPosition(0, 0), new GridPosition(3, 0), 2, _ => true);

        Assert.Equal(MovementPlanStatus.MovementBudgetExceeded, result.Status);
        Assert.Equal(3, result.RequiredSteps);
        Assert.Equal(4, result.Path.Count);
        Assert.Equal(0, result.RemainingMovement);
    }

    [Fact]
    public void Plan_ZeroBudget_AllowsRemainingAtStart()
    {
        var position = new GridPosition(1, 1);

        MovementPlan result = Plan(position, position, 0, _ => true);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.RequiredSteps);
        Assert.Equal([position], result.Path);
    }

    [Fact]
    public void Plan_NegativeBudget_ReturnsStructuredStatusWithoutSearching()
    {
        MovementPlan result = Plan(new GridPosition(0, 0), new GridPosition(1, 0), -1, _ => true);

        Assert.Equal(MovementPlanStatus.InvalidMovementBudget, result.Status);
        Assert.Empty(result.Path);
        Assert.Equal(0, result.VisitedPositions);
    }

    [Theory]
    [InlineData(-1, 0, MovementPlanStatus.StartOutsideGrid)]
    [InlineData(0, -1, MovementPlanStatus.StartOutsideGrid)]
    public void Plan_StartOutsideGrid_MapsSearchStatus(int x, int y, MovementPlanStatus expected)
    {
        MovementPlan result = Plan(new GridPosition(x, y), new GridPosition(1, 1), 10, _ => true);

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public void Plan_BlockedDestination_MapsSearchStatus()
    {
        var destination = new GridPosition(2, 2);

        MovementPlan result = Plan(new GridPosition(0, 0), destination, 10, position => position != destination);

        Assert.Equal(MovementPlanStatus.DestinationBlocked, result.Status);
    }

    [Fact]
    public void Plan_NoPath_MapsSearchStatus()
    {
        MovementPlan result = Plan(
            new GridPosition(0, 0),
            new GridPosition(2, 0),
            10,
            position => position.X != 1);

        Assert.Equal(MovementPlanStatus.NoPath, result.Status);
    }

    [Fact]
    public void Plan_SearchLimitReached_MapsSearchStatus()
    {
        MovementPlan result = planner.Plan(
            size,
            new GridPosition(0, 0),
            new GridPosition(4, 4),
            20,
            OrthogonalGridTopology.Instance,
            _ => true,
            maximumVisitedPositions: 2);

        Assert.Equal(MovementPlanStatus.SearchLimitExceeded, result.Status);
        Assert.Equal(2, result.VisitedPositions);
    }

    private MovementPlan Plan(
        GridPosition start,
        GridPosition destination,
        int budget,
        Func<GridPosition, bool> canEnter) =>
        planner.Plan(size, start, destination, budget, OrthogonalGridTopology.Instance, canEnter);
}
