using System.Collections.Generic;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests.Validation;

public class VoronoiOracleComparisonTests
{
    [Fact]
    public void Triangle_VoronoiNeighborGraph_MatchesOracle()
    {
        // Points form a right triangle.
        var points = new List<OraclePoint>
        {
            new(0.0, 0.0), // 0
            new(1.0, 0.0), // 1
            new(0.0, 1.0), // 2
        };

        // In the unconstrained plane, each generator has the other two as Voronoi neighbors.
        var oracle = new OracleVoronoiOutput(
            Cells: new List<OracleVoronoiCell>
            {
                new(GeneratorIndex: 0, Polygon: new List<OraclePoint>(), Neighbors: new List<int> { 1, 2 }),
                new(GeneratorIndex: 1, Polygon: new List<OraclePoint>(), Neighbors: new List<int> { 0, 2 }),
                new(GeneratorIndex: 2, Polygon: new List<OraclePoint>(), Neighbors: new List<int> { 0, 1 }),
            });

        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        foreach (var p in points)
        {
            triangulation.Insert(new Point2<double>(p.X, p.Y));
        }

        VoronoiOracleComparison.AssertEquivalentNeighborGraph(oracle, points, triangulation);
    }

    [Fact]
    public void Quad_VoronoiNeighborGraph_MatchesOracle()
    {
        var points = new List<OraclePoint>
        {
            new(0.0, 0.0), // 0
            new(1.0, 0.0), // 1
            new(0.0, 1.0), // 2
            new(1.0, 1.0), // 3
        };

        // Neighbor sets derived from the Delaunay triangulation used by Spade
        // for this configuration (diagonal between points 1 and 2):
        // 0: {1, 2}
        // 1: {0, 2, 3}
        // 2: {0, 1, 3}
        // 3: {1, 2}
        var oracle = new OracleVoronoiOutput(
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

        VoronoiOracleComparison.AssertEquivalentNeighborGraph(oracle, points, triangulation);
    }
}
