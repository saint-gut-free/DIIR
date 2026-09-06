using System.Collections.ObjectModel;

namespace DisciplesRemaster.Core.Geometry;

/// <summary>
/// An immutable rectangular grid represented by a default value and sparse overrides.
/// </summary>
public sealed class SparseGrid<T>
{
    private readonly IReadOnlyDictionary<GridPosition, T> overrides;

    public SparseGrid(
        GridSize size,
        T defaultValue,
        IEnumerable<KeyValuePair<GridPosition, T>>? overrides = null)
    {
        Size = size;
        DefaultValue = defaultValue;

        Dictionary<GridPosition, T> values = [];
        foreach (KeyValuePair<GridPosition, T> entry in overrides ?? [])
        {
            if (!size.Contains(entry.Key))
            {
                throw new ArgumentOutOfRangeException(nameof(overrides), "An override position is outside the grid.");
            }

            if (!values.TryAdd(entry.Key, entry.Value))
            {
                throw new ArgumentException("Override positions must be unique.", nameof(overrides));
            }
        }

        this.overrides = new ReadOnlyDictionary<GridPosition, T>(values);
    }

    public GridSize Size { get; }

    public T DefaultValue { get; }

    public IReadOnlyDictionary<GridPosition, T> Overrides => overrides;

    public T this[GridPosition position]
    {
        get
        {
            if (!Size.Contains(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), "Position is outside the grid.");
            }

            return overrides.TryGetValue(position, out T? value) ? value : DefaultValue;
        }
    }
}
