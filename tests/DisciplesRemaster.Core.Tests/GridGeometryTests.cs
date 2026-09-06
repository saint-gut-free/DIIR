using DisciplesRemaster.Core.Geometry;

namespace DisciplesRemaster.Core.Tests;

public sealed class GridGeometryTests
{
    [Fact]
    public void GridSize_WithPositiveDimensions_ComputesCellCount()
    {
        var size = new GridSize(12, 7);

        Assert.Equal(12, size.Width);
        Assert.Equal(7, size.Height);
        Assert.Equal(84, size.CellCount);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public void GridSize_WithNonPositiveDimension_Throws(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GridSize(width, height));
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(4, 2, true)]
    [InlineData(5, 2, false)]
    [InlineData(4, 3, false)]
    [InlineData(-1, 0, false)]
    public void Contains_UsesZeroBasedBounds(int x, int y, bool expected)
    {
        var size = new GridSize(5, 3);

        Assert.Equal(expected, size.Contains(new GridPosition(x, y)));
    }

    [Fact]
    public void SparseGrid_ReturnsDefaultAndOverrides()
    {
        var position = new GridPosition(2, 1);
        var grid = new SparseGrid<string>(
            new GridSize(4, 3),
            "plain",
            [new KeyValuePair<GridPosition, string>(position, "forest")]);

        Assert.Equal("plain", grid[new GridPosition(0, 0)]);
        Assert.Equal("forest", grid[position]);
        Assert.Single(grid.Overrides);
    }

    [Fact]
    public void SparseGrid_RejectsDuplicateOrOutOfBoundsOverrides()
    {
        var position = new GridPosition(1, 1);
        var size = new GridSize(2, 2);

        Assert.Throws<ArgumentException>(() => new SparseGrid<int>(
            size,
            0,
            [
                new KeyValuePair<GridPosition, int>(position, 1),
                new KeyValuePair<GridPosition, int>(position, 2),
            ]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SparseGrid<int>(
            size,
            0,
            [new KeyValuePair<GridPosition, int>(new GridPosition(2, 0), 1)]));
    }

    [Fact]
    public void SparseGrid_RejectsOutOfBoundsLookup()
    {
        var grid = new SparseGrid<int>(new GridSize(2, 2), 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => grid[new GridPosition(-1, 0)]);
    }
}
