namespace DisciplesRemaster.Core.Geometry;

/// <summary>
/// Identifies a zero-based position on an engine-neutral rectangular grid.
/// </summary>
public readonly record struct GridPosition(int X, int Y)
{
    public bool IsInside(GridSize size) =>
        X >= 0 &&
        Y >= 0 &&
        X < size.Width &&
        Y < size.Height;
}
