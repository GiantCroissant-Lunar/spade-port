using System.Collections.Generic;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests.Validation;

public class DelaunayVoronoiOracleCombinedTests
{
    [Fact]
    public void Quad_TriangulationAndVoronoi_MatchOracle()
    {
        var points = new List<OraclePoint>
        {
            new(0.0, 0.0), // 0
            new(1.0, 0.0), // 1
            new(0.0, 1.0), // 2
            new(1.0, 1.0), // 3
        };

        var triOracle = new OracleTriangulationOutput(
            Points: points,
            Triangles: new List<int[]>
            {
                new[] { 0, 1, 2 },
                new[] { 1, 2, 3 },
            });

        var voronoiOracle = new OracleVoronoiOutput(
            Cells: new List<OracleVoronoiCell>
            {
                new(GeneratorIndex: 0, Polygon: new List<OraclePoint>(), Neighbors: new List<int> { 1, 2 }),
                new(GeneratorIndex: 1, Polygon: new List<OraclePoint>(), Neighbors: new List<int> { 0, 2, 3 }),
                new(GeneratorIndex: 2, Polygon: new List<OraclePoint>(), Neighbors: new List<int> { 0, 1, 3 }),
                new(GeneratorIndex: 3, Polygon: new List<OraclePoint>(), Neighbors: new List<int> { 1, 2 }),
            });

        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        foreach (var p in points)
        {
            triangulation.Insert(new Point2<double>(p.X, p.Y));
        }

        TriangulationOracleComparison.AssertEquivalentToOracle(triOracle, triangulation);
        VoronoiOracleComparison.AssertEquivalentNeighborGraph(voronoiOracle, points, triangulation);
    }
}
