using System.Collections.Generic;
using FluentAssertions;
using Spade.Advanced.Interpolation;
using Spade.Primitives;

namespace Spade.Tests;

public class DiscreteGridNaturalNeighbor3DTests
{
    [Fact]
    public void InterpolateToGrid_ConstantField_ProducesConstantEverywhere()
    {
        var points = new List<Point3<double>>
        {
            new Point3<double>(-1.0, -1.0, -1.0),
            new Point3<double>(-1.0, -1.0,  1.0),
            new Point3<double>(-1.0,  1.0, -1.0),
            new Point3<double>(-1.0,  1.0,  1.0),
            new Point3<double>( 1.0, -1.0, -1.0),
            new Point3<double>( 1.0, -1.0,  1.0),
            new Point3<double>( 1.0,  1.0, -1.0),
            new Point3<double>( 1.0,  1.0,  1.0),
        };

        var h = 2.0;
        var values = new List<double>();
        for (int i = 0; i < points.Count; i++)
        {
            values.Add(h);
        }

        var min = new Point3<double>(-1.0, -1.0, -1.0);
        var max = new Point3<double>( 1.0,  1.0,  1.0);

        var nx = 15;
        var ny = 15;
        var nz = 15;
        var grid = DiscreteGridNaturalNeighbor3D.InterpolateToGrid(points, values, nx, ny, nz, min, max);

        var sizeZ = grid.GetLength(0);
        var sizeY = grid.GetLength(1);
        var sizeX = grid.GetLength(2);

        for (int iz = 0; iz < sizeZ; iz++)
        {
            for (int iy = 0; iy < sizeY; iy++)
            {
                for (int ix = 0; ix < sizeX; ix++)
                {
                    grid[iz, iy, ix].Should().BeApproximately(h, 1e-6);
                }
            }
        }
    }

    [Fact]
    public void InterpolateToGrid_SlopeField_MatchesExpectedXAlongCenterLine()
    {
        var points = new List<Point3<double>>();
        var values = new List<double>();

        var coords = new[] { -1.0, -0.5, 0.0, 0.5, 1.0 };
        foreach (var x in coords)
        {
            foreach (var y in coords)
            {
                foreach (var z in coords)
                {
                    points.Add(new Point3<double>(x, y, z));
                    values.Add(x);
                }
            }
        }

        var min = new Point3<double>(-1.0, -1.0, -1.0);
        var max = new Point3<double>( 1.0,  1.0,  1.0);

        var nx = 41;
        var ny = 41;
        var nz = 41;
        var grid = DiscreteGridNaturalNeighbor3D.InterpolateToGrid(points, values, nx, ny, nz, min, max);

        var centerY = ny / 2;
        var centerZ = nz / 2;
        var stepX = (max.X - min.X) / (nx - 1);

        for (int ix = 0; ix < nx; ix++)
        {
            var expectedX = min.X + ix * stepX;
            grid[centerZ, centerY, ix].Should().BeApproximately(expectedX, 0.25);
        }
    }
}
