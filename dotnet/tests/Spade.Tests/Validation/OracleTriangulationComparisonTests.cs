using System.Collections.Generic;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests.Validation;

public class OracleTriangulationComparisonTests
{
    [Fact]
    public void SimpleTriangle_MatchesOracleTriangulationOutput()
    {
        var oracle = new OracleTriangulationOutput(
            Points: new List<OraclePoint>
            {
                new(0.0, 0.0),
                new(1.0, 0.0),
                new(0.0, 1.0),
            },
            Triangles: new List<int[]>
            {
                new[] { 0, 1, 2 },
            });

        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        foreach (var p in oracle.Points)
        {
            triangulation.Insert(new Point2<double>(p.X, p.Y));
        }

        TriangulationOracleComparison.AssertEquivalentToOracle(oracle, triangulation);
    }

    [Fact]
    public void Quad_MatchesOracleTriangulationOutput()
    {
        var oracle = new OracleTriangulationOutput(
            Points: new List<OraclePoint>
            {
                new(0.0, 0.0), // 0
                new(1.0, 0.0), // 1
                new(0.0, 1.0), // 2
                new(1.0, 1.0), // 3
            },
            Triangles: new List<int[]>
            {
                new[] { 0, 1, 2 },
                new[] { 1, 2, 3 },
            });

        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        foreach (var p in oracle.Points)
        {
            triangulation.Insert(new Point2<double>(p.X, p.Y));
        }

        TriangulationOracleComparison.AssertEquivalentToOracle(oracle, triangulation);
    }
}
