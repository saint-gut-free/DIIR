namespace DisciplesRemaster.Core.Geometry;

/// <summary>
/// Defines candidate neighbors independently from movement and terrain rules.
/// </summary>
public interface IGridTopology
{
    IEnumerable<GridPosition> GetNeighbors(GridPosition position);
}
