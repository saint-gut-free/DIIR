namespace DisciplesRemaster.Core.Geometry;

/// <summary>
/// Describes the dimensions of an engine-neutral rectangular grid.
/// </summary>
public readonly record struct GridSize
{
    public GridSize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }

    public long CellCount => (long)Width * Height;

    public bool Contains(GridPosition position) => position.IsInside(this);
}
