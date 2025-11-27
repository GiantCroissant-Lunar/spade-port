using System.Collections.Generic;
using FluentAssertions;
using Spade.Advanced.Interpolation;
using Spade.Primitives;
using Xunit;

namespace Spade.Advanced.Tests;

public class GridNaturalNeighbor2DTests
{
    [Fact]
    public void ExactGrid_ConstantField_ReturnsConstantEverywhere()
    {
        var samplePoints = new List<Point2<double>>
        {
            new(-1.0, -1.0),
            new(-1.0,  1.0),
            new( 1.0, -1.0),
            new( 1.0,  1.0),
        };

        var h = 1.5;
        var sampleValues = new List<double> { h, h, h, h };

        var min = new Point2<double>(-1.0, -1.0);
        var max = new Point2<double>( 1.0,  1.0);

        var grid = NaturalNeighborGrid2D.Exact(
            samplePoints,
            sampleValues,
            width: 5,
            height: 5,
            min,
            max);

        for (int iy = 0; iy < grid.GetLength(0); iy++)
        {
            for (int ix = 0; ix < grid.GetLength(1); ix++)
            {
                grid[iy, ix].Should().BeApproximately(h, 1e-6);
            }
        }
    }

    [Fact]
    public void DiscreteGrid_ConstantField_ReturnsConstantInsideDomain()
    {
        var samplePoints = new List<Point2<double>>
        {
            new(-1.0, -1.0),
            new(-1.0,  1.0),
            new( 1.0, -1.0),
            new( 1.0,  1.0),
        };

        var h = 2.0;
        var sampleValues = new List<double> { h, h, h, h };

        var min = new Point2<double>(-1.0, -1.0);
        var max = new Point2<double>( 1.0,  1.0);

        var grid = NaturalNeighborGrid2D.Discrete(
            samplePoints,
            sampleValues,
            width: 5,
            height: 5,
            min,
            max);

        for (int iy = 0; iy < grid.GetLength(0); iy++)
        {
            for (int ix = 0; ix < grid.GetLength(1); ix++)
            {
                grid[iy, ix].Should().BeApproximately(h, 1e-6);
            }
        }
    }
}
