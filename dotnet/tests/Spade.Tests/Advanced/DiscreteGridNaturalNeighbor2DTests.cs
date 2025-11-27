using System.Collections.Generic;
using FluentAssertions;
using Spade.Advanced.Interpolation;
using Spade.Primitives;

namespace Spade.Tests;

public class DiscreteGridNaturalNeighbor2DTests
{
    [Fact]
    public void InterpolateToGrid_ConstantField_ProducesConstantEverywhere()
    {
        var points = new List<Point2<double>>
        {
            new Point2<double>(-1.0, -1.0),
            new Point2<double>(-1.0, 1.0),
            new Point2<double>(1.0, -1.0),
            new Point2<double>(1.0, 1.0),
        };

        var h = 3.0;
        var values = new List<double> { h, h, h, h };

        var min = new Point2<double>(-1.0, -1.0);
        var max = new Point2<double>(1.0, 1.0);

        var grid = DiscreteGridNaturalNeighbor2D.InterpolateToGrid(points, values, width: 21, height: 21, min, max);

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
    public void InterpolateToGrid_SlopeField_MatchesExpectedXAlongCenterRow()
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

        var width = 41;
        var height = 41;
        var grid = DiscreteGridNaturalNeighbor2D.InterpolateToGrid(points, values, width, height, min, max);

        var centerRow = height / 2;
        var stepX = (max.X - min.X) / (width - 1);

        for (int ix = 0; ix < width; ix++)
        {
            var expectedX = min.X + ix * stepX;
            grid[centerRow, ix].Should().BeApproximately(expectedX, 0.15);
        }
    }
}
