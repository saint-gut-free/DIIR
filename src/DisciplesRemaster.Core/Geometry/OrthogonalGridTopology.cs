namespace DisciplesRemaster.Core.Geometry;

/// <summary>
/// Project-owned four-directional square-grid topology with deterministic ordering.
/// </summary>
public sealed class OrthogonalGridTopology : IGridTopology
{
    public static OrthogonalGridTopology Instance { get; } = new();

    private OrthogonalGridTopology()
    {
    }

    public IEnumerable<GridPosition> GetNeighbors(GridPosition position)
    {
        yield return position with { Y = position.Y - 1 };
        yield return position with { X = position.X - 1 };
        yield return position with { X = position.X + 1 };
        yield return position with { Y = position.Y + 1 };
    }
}
