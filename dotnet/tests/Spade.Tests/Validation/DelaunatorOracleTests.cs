using System.Collections.Generic;
using System.Linq;
using Spade;
using Spade.Primitives;
using VoronatorSharp;
using Xunit;

namespace Spade.Tests.Validation;

public class DelaunatorOracleTests
{
    [Fact]
    public void DelaunatorSamplePoints_MatchSpadeTriangulation()
    {
        // Sample from Delaunator README
        var points = new List<OraclePoint>
        {
            new(377.0, 479.0),
            new(453.0, 434.0),
            new(326.0, 387.0),
            new(444.0, 359.0),
            new(511.0, 389.0),
            new(586.0, 429.0),
            new(470.0, 315.0),
            new(622.0, 493.0),
            new(627.0, 367.0),
            new(570.0, 314.0),
        };

        // Build oracle triangulation using VoronatorSharp's Delaunator port.
        var delaunatorPoints = points
            .Select(p => new Vector2((float)p.X, (float)p.Y))
            .ToList();

        var delaunator = new Delaunator(delaunatorPoints);

        var oracleTriangles = new List<int[]>();
        var triArray = delaunator.Triangles;
        for (var i = 0; i < triArray.Length; i += 3)
        {
            oracleTriangles.Add(new[]
            {
                triArray[i],
                triArray[i + 1],
                triArray[i + 2],
            });
        }

        var oracle = new OracleTriangulationOutput(points, oracleTriangles);

        // Build Spade triangulation over the same points.
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        foreach (var p in points)
        {
            triangulation.Insert(new Point2<double>(p.X, p.Y));
        }

        // Compare Spade's output against the Delaunator-based oracle.
        TriangulationOracleComparison.AssertEquivalentToOracle(oracle, triangulation);
    }
}
