using System.Collections.Generic;
using FluentAssertions;
using Spade.Advanced.Interpolation;
using Spade.Primitives;

namespace Spade.Tests;

public class GridNaturalNeighbor2DTests
{
    [Fact]
    public void InterpolateToGrid_ConstantField_ProducesConstantInsideHull()
    {
        var points = new List<Point2<double>>
        {
            new Point2<double>(-1.0, -1.0),
            new Point2<double>(-1.0, 1.0),
            new Point2<double>(1.0, -1.0),
            new Point2<double>(1.0, 1.0),
        };

        var h = 2.5;
        var values = new List<double> { h, h, h, h };

        var min = new Point2<double>(-1.0, -1.0);
        var max = new Point2<double>(1.0, 1.0);

        var grid = GridNaturalNeighbor2D.InterpolateToGrid(points, values, width: 5, height: 5, min, max);

        var height = grid.GetLength(0);
        var width = grid.GetLength(1);

        for (int iy = 0; iy < height; iy++)
        {
            for (int ix = 0; ix < width; ix++)
            {
                grid[iy, ix].Should().BeApproximately(h, 1e-6);
            }
        }
    }

    [Fact]
    public void InterpolateToGrid_SlopeField_MatchesExpectedXWithinHull()
    {
        var points = new List<Point2<double>>();
        var values = new List<double>();

        var coords = new[] { -1.0, -0.5, 0.0, 0.5, 1.0 };
        foreach (var x in coords)
        {
            foreach (var y in coords)
            {
                points.Add(new Point2<double>(x, y));
                values.Add(x);
            }
        }

        var min = new Point2<double>(-1.0, -1.0);
        var max = new Point2<double>(1.0, 1.0);

        var width = 7;
        var height = 7;
        var grid = GridNaturalNeighbor2D.InterpolateToGrid(points, values, width, height, min, max);

        var dx = (max.X - min.X) / (width - 1);

        for (int ix = 0; ix < width; ix++)
        {
            var expectedX = min.X + ix * dx;

            for (int iy = 1; iy < height - 1; iy++)
            {
                grid[iy, ix].Should().BeApproximately(expectedX, 1e-2);
            }
        }
    }
}
