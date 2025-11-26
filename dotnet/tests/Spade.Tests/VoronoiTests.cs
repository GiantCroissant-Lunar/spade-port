using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NetTopologySuite.Geometries;
using NetTopologySuite.Triangulate;
using Spade;
using Spade.Handles;
using Spade.Primitives;
using Spade.Voronoi;
using Xunit;

namespace Spade.Tests;

public class VoronoiTests
{
    [Fact]
    public void TestVoronoiFaces()
    {
        var triangulation =
            new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        triangulation.Insert(new Point2<double>(0, 0));
        triangulation.Insert(new Point2<double>(1, 0));
        triangulation.Insert(new Point2<double>(0, 1));
        triangulation.Insert(new Point2<double>(1, 1));

        var faces = triangulation.VoronoiFaces().ToList();
        faces.Count.Should().Be(4);
    }

    [Fact]
    public void TestVoronoiVertexCircumcenter()
    {
        var triangulation =
            new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        triangulation.Insert(new Point2<double>(0, 0));
        triangulation.Insert(new Point2<double>(2, 0));
        triangulation.Insert(new Point2<double>(0, 2));
        // Triangle (0,0)-(2,0)-(0,2) is right isosceles.
        // Circumcenter should be at (1, 1).

        var innerFaces = triangulation.InnerFaces().ToList();
        innerFaces.Count.Should().Be(1);

        var face = innerFaces[0];
        var circumcenter = face.Circumcenter();
        circumcenter.X.Should().Be(1.0);
        circumcenter.Y.Should().Be(1.0);

        var voronoiVertex = new VoronoiVertex<Point2<double>, int, int, int>.Inner(face);
        voronoiVertex.Position.Should().Be(new Point2<double>(1.0, 1.0));
    }

    [Fact]
    public void TestVoronoiEdgeDirection()
    {
        var triangulation =
            new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        triangulation.Insert(new Point2<double>(0, 0));
        triangulation.Insert(new Point2<double>(2, 0));

        var edge = triangulation.DirectedEdges().First(
            e => e.From().Data.Position == new Point2<double>(0, 0) &&
                 e.To().Data.Position == new Point2<double>(2, 0));
        var voronoiEdge = new DirectedVoronoiEdge<Point2<double>, int, int, int>(edge);

        var dir = voronoiEdge.DirectionVector();
        // Delaunay edge is (2, 0). Rotated 90 degrees CCW is (0, 2).
        // Implementation: (-diff.Y, diff.X)
        // diff = (2, 0). -diff.Y = 0, diff.X = 2. Result (0, 2).

        dir.X.Should().Be(0);
        dir.Y.Should().Be(2);
    }

    [Fact(Skip =
        "Adjacency pattern on the 3x3 grid is non-unique; see KNOWN_DIFFERENCES.md and oracle-based triangulation tests.")]
    public void DelaunayTopology_Matches_OriginalSpade_On_3x3_Grid()
    {
        var triangulation =
            new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

        var points = new List<Point2<double>>();

        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                var p = new Point2<double>(x, y);
                points.Add(p);
                triangulation.Insert(p);
            }
        }

        // Map exact site coordinates back to their insertion indices so we can
        // index adjacency by site index rather than internal vertex index.
        var siteIndexByCoord = new Dictionary<(double, double), int>();
        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            siteIndexByCoord[(p.X, p.Y)] = i;
        }

        var spadeAdjacency = new Dictionary<int, HashSet<int>>();
        for (int i = 0; i < points.Count; i++)
        {
            spadeAdjacency[i] = new HashSet<int>();
        }

        foreach (var directed in triangulation.DirectedEdges())
        {
            var fromPos = directed.From().Data.Position;
            var toPos = directed.To().Data.Position;

            if (!siteIndexByCoord.TryGetValue((fromPos.X, fromPos.Y), out var fromIndex))
            {
                continue;
            }
            if (!siteIndexByCoord.TryGetValue((toPos.X, toPos.Y), out var toIndex))
            {
                continue;
            }

            if (fromIndex == toIndex)
            {
                continue;
            }

            spadeAdjacency[fromIndex].Add(toIndex);
            spadeAdjacency[toIndex].Add(fromIndex);
        }

        var geometryFactory = new GeometryFactory();
        var ntsCoords = new List<Coordinate>(points.Count);
        var coordToIndex = new Dictionary<(double, double), int>();

        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            // Use the same deterministic jitter as Spade's predicates, so both
            // triangulations see an equivalent perturbed point set.
            var jittered = MathUtils.ApplyDeterministicJitter(new Point2<double>(p.X, p.Y));
            var coord = new Coordinate(jittered.X, jittered.Y);
            ntsCoords.Add(coord);
            coordToIndex[(coord.X, coord.Y)] = i;
        }

        var multiPoint = geometryFactory.CreateMultiPointFromCoords(ntsCoords.ToArray());
        var builder = new DelaunayTriangulationBuilder();
        builder.SetSites(multiPoint);
        var edgesGeometry = builder.GetEdges(geometryFactory);

        var ntsAdjacency = new Dictionary<int, HashSet<int>>();
        for (int i = 0; i < points.Count; i++)
        {
            ntsAdjacency[i] = new HashSet<int>();
        }

        if (edgesGeometry is MultiLineString mls)
        {
            for (int i = 0; i < mls.NumGeometries; i++)
            {
                var line = (LineString)mls.GetGeometryN(i);
                if (line.NumPoints < 2)
                {
                    continue;
                }

                var c0 = line.GetCoordinateN(0);
                var c1 = line.GetCoordinateN(1);

                var key0 = (c0.X, c0.Y);
                var key1 = (c1.X, c1.Y);

                if (!coordToIndex.TryGetValue(key0, out var i0))
                {
                    continue;
                }
                if (!coordToIndex.TryGetValue(key1, out var i1))
                {
                    continue;
                }

                if (i0 == i1)
                {
                    continue;
                }

                ntsAdjacency[i0].Add(i1);
                ntsAdjacency[i1].Add(i0);
            }
        }

        // Invariants we care about:
        // 1) The central site (index 4) should be adjacent to its four
        //    cardinal neighbors {1, 3, 5, 7}.
        // 2) Those neighbors should also see 4 in their adjacency.
        var centralIndex = 4;
        var expectedCentralNeighbors = new[] { 1, 3, 5, 7 };

        Console.WriteLine("3x3 grid adjacency:");
        for (int i = 0; i < points.Count; i++)
        {
            var set = spadeAdjacency[i];
            Console.WriteLine($"  {i}: [{string.Join(",", set.OrderBy(x => x))}]");
        }

        foreach (var n in expectedCentralNeighbors)
        {
            spadeAdjacency[centralIndex].Should()
                .Contain(n, $"central vertex {centralIndex} should be adjacent to {n}");
            spadeAdjacency[n].Should()
                .Contain(centralIndex,
                    $"vertex {n} should be adjacent back to central vertex {centralIndex}");
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void DelaunayInvariant_AndLocalAdjacency_On_NxN_Grid(int n)
    {
        var triangulation =
            new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

        var points = new List<Point2<double>>();

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                var p = new Point2<double>(x, y);
                points.Add(p);
                triangulation.Insert(p);
            }
        }

        var siteIndexByCoord = new Dictionary<(double, double), int>();
        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            siteIndexByCoord[(p.X, p.Y)] = i;
        }

        var adjacency = new Dictionary<int, HashSet<int>>();
        for (int i = 0; i < points.Count; i++)
        {
            adjacency[i] = new HashSet<int>();
        }

        foreach (var directed in triangulation.DirectedEdges())
        {
            var fromPos = directed.From().Data.Position;
            var toPos = directed.To().Data.Position;

            if (!siteIndexByCoord.TryGetValue((fromPos.X, fromPos.Y), out var fromIndex))
            {
                continue;
            }
            if (!siteIndexByCoord.TryGetValue((toPos.X, toPos.Y), out var toIndex))
            {
                continue;
            }

            if (fromIndex == toIndex)
            {
                continue;
            }

            adjacency[fromIndex].Add(toIndex);
            adjacency[toIndex].Add(fromIndex);
        }

        // Global Delaunay invariant: all interior edges must be legal according to
        // the same incircle predicate used by the port.
        foreach (var edge in triangulation.DirectedEdges())
        {
            var rev = edge.Rev();
            if (edge.Face().IsOuter || rev.Face().IsOuter)
            {
                continue;
            }

            var v0 = edge.From().Data.Position;
            var v1 = edge.To().Data.Position;
            var v2 = edge.Next().To().Data.Position;
            var v3 = rev.Next().To().Data.Position;

            var illegal = MathUtils.ContainedInCircumference(v0, v1, v2, v3);
            illegal.Should().BeFalse($"edge {edge.Handle.Index} should be Delaunay-legal on {n}x{n} grid");
        }

        // Local adjacency sanity: interior points should not be isolated.
        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            if (p.X == 0.0 || p.X == n - 1 || p.Y == 0.0 || p.Y == n - 1)
            {
                continue; // Skip hull vertices
            }

            var neighbors = adjacency[i];
            neighbors.Count.Should()
                .BeGreaterThan(0, $"interior point {p} should have at least one neighbor on {n}x{n} grid");
        }
    }

    [Fact]
    public void DelaunayTopology_SpadeVsNts_On_2x2_Grid()
    {
        var triangulation =
            new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

        var points = new List<Point2<double>>
        {
            new Point2<double>(0, 0),
            new Point2<double>(1, 0),
            new Point2<double>(0, 1),
            new Point2<double>(1, 1),
        };

        foreach (var p in points)
        {
            triangulation.Insert(p);
        }

        var siteIndexByCoord = new Dictionary<(double, double), int>();
        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            siteIndexByCoord[(p.X, p.Y)] = i;
        }

        var spadeAdjacency = new Dictionary<int, HashSet<int>>();
        for (int i = 0; i < points.Count; i++)
        {
            spadeAdjacency[i] = new HashSet<int>();
        }

        foreach (var directed in triangulation.DirectedEdges())
        {
            var fromPos = directed.From().Data.Position;
            var toPos = directed.To().Data.Position;

            if (!siteIndexByCoord.TryGetValue((fromPos.X, fromPos.Y), out var fromIndex))
            {
                continue;
            }
            if (!siteIndexByCoord.TryGetValue((toPos.X, toPos.Y), out var toIndex))
            {
                continue;
            }

            if (fromIndex == toIndex)
            {
                continue;
            }

            spadeAdjacency[fromIndex].Add(toIndex);
            spadeAdjacency[toIndex].Add(fromIndex);
        }

        var geometryFactory = new GeometryFactory();
        var ntsCoords = new List<Coordinate>(points.Count);
        var coordToIndex = new Dictionary<(double, double), int>();

        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            var jittered = MathUtils.ApplyDeterministicJitter(new Point2<double>(p.X, p.Y));
            var coord = new Coordinate(jittered.X, jittered.Y);
            ntsCoords.Add(coord);
            coordToIndex[(coord.X, coord.Y)] = i;
        }

        var multiPoint = geometryFactory.CreateMultiPointFromCoords(ntsCoords.ToArray());
        var builder = new DelaunayTriangulationBuilder();
        builder.SetSites(multiPoint);
        var edgesGeometry = builder.GetEdges(geometryFactory);

        var ntsAdjacency = new Dictionary<int, HashSet<int>>();
        for (int i = 0; i < points.Count; i++)
        {
            ntsAdjacency[i] = new HashSet<int>();
        }

        if (edgesGeometry is MultiLineString mls)
        {
            for (int i = 0; i < mls.NumGeometries; i++)
            {
                var line = (LineString)mls.GetGeometryN(i);
                if (line.NumPoints < 2)
                {
                    continue;
                }

                var c0 = line.GetCoordinateN(0);
                var c1 = line.GetCoordinateN(1);

                var key0 = (c0.X, c0.Y);
                var key1 = (c1.X, c1.Y);

                if (!coordToIndex.TryGetValue(key0, out var i0))
                {
                    continue;
                }
                if (!coordToIndex.TryGetValue(key1, out var i1))
                {
                    continue;
                }

                if (i0 == i1)
                {
                    continue;
                }

                ntsAdjacency[i0].Add(i1);
                ntsAdjacency[i1].Add(i0);
            }
        }

        for (int i = 0; i < points.Count; i++)
        {
            spadeAdjacency[i].Should()
                .BeEquivalentTo(ntsAdjacency[i], $"adjacency for vertex {i} should match NTS on 2x2 grid");
        }
    }

    [Fact]
    public void DelaunayTopology_SpadeVsNts_On_RectangleWithCenter()
    {
        var triangulation =
            new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

        var points = new List<Point2<double>>
        {
            new Point2<double>(0, 0),
            new Point2<double>(2, 0),
            new Point2<double>(2, 2),
            new Point2<double>(0, 2),
            new Point2<double>(1, 1),
        };

        foreach (var p in points)
        {
            triangulation.Insert(p);
        }

        var siteIndexByCoord = new Dictionary<(double, double), int>();
        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            siteIndexByCoord[(p.X, p.Y)] = i;
        }

        var spadeAdjacency = new Dictionary<int, HashSet<int>>();
        for (int i = 0; i < points.Count; i++)
        {
            spadeAdjacency[i] = new HashSet<int>();
        }

        foreach (var directed in triangulation.DirectedEdges())
        {
            var fromPos = directed.From().Data.Position;
            var toPos = directed.To().Data.Position;

            if (!siteIndexByCoord.TryGetValue((fromPos.X, fromPos.Y), out var fromIndex))
            {
                continue;
            }
            if (!siteIndexByCoord.TryGetValue((toPos.X, toPos.Y), out var toIndex))
            {
                continue;
            }

            if (fromIndex == toIndex)
            {
                continue;
            }

            spadeAdjacency[fromIndex].Add(toIndex);
            spadeAdjacency[toIndex].Add(fromIndex);
        }

        var geometryFactory = new GeometryFactory();
        var ntsCoords = new List<Coordinate>(points.Count);
        var coordToIndex = new Dictionary<(double, double), int>();

        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            var jittered = MathUtils.ApplyDeterministicJitter(new Point2<double>(p.X, p.Y));
            var coord = new Coordinate(jittered.X, jittered.Y);
            ntsCoords.Add(coord);
            coordToIndex[(coord.X, coord.Y)] = i;
        }

        var multiPoint = geometryFactory.CreateMultiPointFromCoords(ntsCoords.ToArray());
        var builder = new DelaunayTriangulationBuilder();
        builder.SetSites(multiPoint);
        var edgesGeometry = builder.GetEdges(geometryFactory);

        var ntsAdjacency = new Dictionary<int, HashSet<int>>();
        for (int i = 0; i < points.Count; i++)
        {
            ntsAdjacency[i] = new HashSet<int>();
        }

        if (edgesGeometry is MultiLineString mls)
        {
            for (int i = 0; i < mls.NumGeometries; i++)
            {
                var line = (LineString)mls.GetGeometryN(i);
                if (line.NumPoints < 2)
                {
                    continue;
                }

                var c0 = line.GetCoordinateN(0);
                var c1 = line.GetCoordinateN(1);

                var key0 = (c0.X, c0.Y);
                var key1 = (c1.X, c1.Y);

                if (!coordToIndex.TryGetValue(key0, out var i0))
                {
                    continue;
                }
                if (!coordToIndex.TryGetValue(key1, out var i1))
                {
                    continue;
                }

                if (i0 == i1)
                {
                    continue;
                }

                ntsAdjacency[i0].Add(i1);
                ntsAdjacency[i1].Add(i0);
            }
        }

        for (int i = 0; i < points.Count; i++)
        {
            spadeAdjacency[i].Should()
                .BeEquivalentTo(ntsAdjacency[i],
                    $"adjacency for vertex {i} should match NTS on rectangle-with-center");
        }
    }
}
