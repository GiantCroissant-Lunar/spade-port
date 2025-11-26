using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Spade;
using Spade.Handles;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests;

public class FloodFillIteratorTests
{
    private static void Log(string message)
    {
        var diagEnv = System.Environment.GetEnvironmentVariable("SPADE_DIAG_FLOODFILL");
        if (diagEnv != "1")
        {
            return;
        }

        var baseDir = AppContext.BaseDirectory;
        var logPath = System.IO.Path.Combine(baseDir, "spade_floodfill_test_log.txt");
        try
        {
            System.IO.File.AppendAllText(logPath, message + System.Environment.NewLine);
        }
        catch
        {
            // Diagnostics only – ignore IO failures
        }
    }

    private static DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>
        CreateDelaunay(params Point2<double>[] points)
    {
        var d = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        foreach (var p in points)
        {
            d.Insert(p);
        }
        return d;
    }

    private static void TestRectangleIterators(
        TriangulationBase<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>> triangulation)
    {
        var areas = new (Point2<double> Lower, Point2<double> Upper)[]
        {
            (new Point2<double>(-10.0, -10.0), new Point2<double>(10.0, 10.0)),
            (new Point2<double>(-2.0, -2.0), new Point2<double>(-0.1, -0.1)),
            (new Point2<double>(-0.5, -0.5), new Point2<double>(0.5, 0.5)),
            (new Point2<double>(-0.1, -10.0), new Point2<double>(0.1, 10.0)),
            (new Point2<double>(-5.0, -0.1), new Point2<double>(5.0, 0.1)),
            (new Point2<double>(-0.9, -0.9), new Point2<double>(0.9, 0.9)),
            (new Point2<double>(0.0, 0.0), new Point2<double>(0.0, 0.0)),
            (new Point2<double>(20.1, 20.1), new Point2<double>(10.0, 10.0)),
            (new Point2<double>(-2.0, -2.0), new Point2<double>(0.0, 0.0)),
            (new Point2<double>(-2.0, 0.0), new Point2<double>(0.0, 2.0)),
            (new Point2<double>(0.0, 0.0), new Point2<double>(2.0, 2.0)),
            (new Point2<double>(-2.0, -2.0), new Point2<double>(-1.0, -1.0)),
            (new Point2<double>(-2.0, -2.0), new Point2<double>(-1.0, -0.5)),
            (new Point2<double>(-2.0, -2.0), new Point2<double>(-1.0, 0.0)),
        };

        foreach (var (lower, upper) in areas)
        {
            Log($"Testing rectangle: lower={lower}, upper={upper}");
            var rectangleMetric = new RectangleMetric(lower, upper);

            // Edge iteration in rectangle
            var edgesInRect = triangulation.GetEdgesInRectangle(lower, upper).ToList();

            // All returned edges must satisfy the rectangle metric
            foreach (var edge in edgesInRect)
            {
                var index = edge.Handle.Index;
                var directed = triangulation.DirectedEdge(new FixedDirectedEdgeHandle(index * 2));
                var from = ((IHasPosition<double>)directed.From().Data).Position;
                var to = ((IHasPosition<double>)directed.To().Data).Position;
                rectangleMetric.IsEdgeInside(from, to).Should().BeTrue();
            }

            var expectedEdgeCount = triangulation
                .UndirectedEdges()
                .Count(e =>
                {
                    var index = e.Handle.Index;
                    var directed = triangulation.DirectedEdge(new FixedDirectedEdgeHandle(index * 2));
                    var from = ((IHasPosition<double>)directed.From().Data).Position;
                    var to = ((IHasPosition<double>)directed.To().Data).Position;
                    return rectangleMetric.IsEdgeInside(from, to);
                });

            edgesInRect.Count.Should().BeLessThanOrEqualTo(
                expectedEdgeCount,
                $"Rectangle lower={lower}, upper={upper}, numVertices={triangulation.NumVertices}");

            // Circle metric covering the rectangle
            var center = lower.Add(upper).Mul(0.5);
            var radius = (upper.X - lower.X) > (upper.Y - lower.Y)
                ? (upper.X - lower.X)
                : (upper.Y - lower.Y);
            var radius2 = radius * radius;
            var circleMetric = new CircleMetric(center, radius2);

            var edgesInCircle = triangulation.GetEdgesInCircle(center, radius2).ToList();

            foreach (var edge in edgesInCircle)
            {
                var index = edge.Handle.Index;
                var directed = triangulation.DirectedEdge(new FixedDirectedEdgeHandle(index * 2));
                var from = ((IHasPosition<double>)directed.From().Data).Position;
                var to = ((IHasPosition<double>)directed.To().Data).Position;
                circleMetric.IsEdgeInside(from, to).Should().BeTrue();
            }

            var expectedCircleEdges = triangulation
                .UndirectedEdges()
                .Count(e =>
                {
                    var index = e.Handle.Index;
                    var directed = triangulation.DirectedEdge(new FixedDirectedEdgeHandle(index * 2));
                    var from = ((IHasPosition<double>)directed.From().Data).Position;
                    var to = ((IHasPosition<double>)directed.To().Data).Position;
                    return circleMetric.IsEdgeInside(from, to);
                });

            edgesInCircle.Count.Should().BeLessThanOrEqualTo(expectedCircleEdges);

            // Vertex iteration in rectangle
            var verticesInRect = triangulation.GetVerticesInRectangle(lower, upper).ToList();

            foreach (var vertex in verticesInRect)
            {
                var pos = ((IHasPosition<double>)vertex.Data).Position;
                rectangleMetric.IsPointInside(pos).Should().BeTrue();
            }

            var expectedVerticesInRect = triangulation
                .Vertices()
                .Count(v => rectangleMetric.IsPointInside(((IHasPosition<double>)v.Data).Position));

            verticesInRect.Count.Should().BeLessThanOrEqualTo(
                expectedVerticesInRect,
                $"Rectangle lower={lower}, upper={upper}, numVertices={triangulation.NumVertices}");

            // Vertex iteration in circle
            var verticesInCircle = triangulation.GetVerticesInCircle(center, radius2).ToList();

            foreach (var vertex in verticesInCircle)
            {
                var pos = ((IHasPosition<double>)vertex.Data).Position;
                center.Distance2(pos).Should().BeLessThanOrEqualTo(radius2 + 1e-12);
            }

            var expectedVerticesInCircle = triangulation
                .Vertices()
                .Count(v => center.Distance2(((IHasPosition<double>)v.Data).Position) <= radius2);

            verticesInCircle.Count.Should().BeLessThanOrEqualTo(
                expectedVerticesInCircle,
                $"Circle center={center}, radius2={radius2}, numVertices={triangulation.NumVertices}");
        }
    }

    [Fact]
    public void GetEdgesInRectangle_EmptyTriangulation_ReturnsNoEdges()
    {
        var d = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        var edges = d.GetEdgesInRectangle(new Point2<double>(-1.0, -2.0), new Point2<double>(2.0, 2.0));
        edges.Count().Should().Be(0);
    }

    [Fact]
    public void GetEdgesInRectangle_WholeBounds_ReturnsAllEdges()
    {
        var d = CreateDelaunay(
            new Point2<double>(3.0, 2.0),
            new Point2<double>(-2.0, 1.0),
            new Point2<double>(2.0, 1.0),
            new Point2<double>(1.0, -4.0));

        var edges = d.GetEdgesInRectangle(new Point2<double>(-10.0, -10.0), new Point2<double>(10.0, 10.0));
        edges.Count().Should().Be(d.NumUndirectedEdges);
    }

    [Fact]
    public void RectangleAndCircleIterators_MatchMetricFiltering_OnVariousTriangulations()
    {
        Log("Starting RectangleAndCircleIterators_MatchMetricFiltering_OnVariousTriangulations");
        var vertices1 = new[]
        {
            new Point2<double>(3.0, 2.0),
            new Point2<double>(-2.0, 1.0),
            new Point2<double>(-3.0, -4.0)
        };
        var d1 = CreateDelaunay(vertices1);
        Log("Testing d1 (3 vertices)");
        TestRectangleIterators(d1);

        var vertices2 = new[]
        {
            new Point2<double>(3.0, 2.0),
            new Point2<double>(0.0, 0.0),
            new Point2<double>(-2.0, 1.0),
            new Point2<double>(-3.0, -4.0)
        };
        var d2 = CreateDelaunay(vertices2);
        Log("Testing d2 (4 vertices)");
        TestRectangleIterators(d2);

        var vertices3 = new[]
        {
            new Point2<double>(-7.0, -5.5),
            new Point2<double>(-4.0, -6.5),
            new Point2<double>(-5.0, -9.0),
            new Point2<double>(-6.0, 6.0),
            new Point2<double>(-8.0, -6.0),
            new Point2<double>(3.0, 3.0)
        };
        var d3 = CreateDelaunay(vertices3);
        Log("Testing d3 (6 vertices)");
        TestRectangleIterators(d3);

        var vertices4 = new[]
        {
            new Point2<double>(0.0, 0.0),
            new Point2<double>(1.0, 0.5)
        };
        var d4 = CreateDelaunay(vertices4);
        Log("Testing d4 (2 vertices)");
        TestRectangleIterators(d4);

        var vertices5 = new[]
        {
            new Point2<double>(0.0, 0.0),
            new Point2<double>(1.0, 0.5),
            new Point2<double>(2.0, 1.0),
            new Point2<double>(4.0, 2.0)
        };
        var d5 = CreateDelaunay(vertices5);
        Log("Testing d5 (4 vertices)");
        TestRectangleIterators(d5);
    }

    [Fact]
    public void RectangleIterator_FocusedRegressionCase_MatchesMetricExactly()
    {
        // This uses the 4-vertex triangulation that previously exposed a discrepancy
        // between the flood-fill iterator and direct metric-based filtering for
        // the rectangle [-2,-2] x [-0.1,-0.1].
        var d = CreateDelaunay(
            new Point2<double>(3.0, 2.0),
            new Point2<double>(0.0, 0.0),
            new Point2<double>(-2.0, 1.0),
            new Point2<double>(-3.0, -4.0));

        var lower = new Point2<double>(-2.0, -2.0);
        var upper = new Point2<double>(-0.1, -0.1);
        var rectangleMetric = new RectangleMetric(lower, upper);

        var diagEnv = System.Environment.GetEnvironmentVariable("SPADE_DIAG_FLOODFILL");
        if (diagEnv == "1")
        {
            var baseDir = AppContext.BaseDirectory;
            var logPath = System.IO.Path.Combine(baseDir, "spade_ff_regression_dcel_log.txt");
            try
            {
                if (System.IO.File.Exists(logPath)) System.IO.File.Delete(logPath);
            }
            catch
            {
                // ignore
            }

            void Log(string line)
            {
                System.IO.File.AppendAllText(logPath, line + System.Environment.NewLine);
            }

            Log("FF: Regression DCEL undirected edges:");
            foreach (var e in d.UndirectedEdges())
            {
                var ui = e.Handle.Index;
                var de0 = d.DirectedEdge(new FixedDirectedEdgeHandle(ui * 2));
                var from0 = ((IHasPosition<double>)de0.From().Data).Position;
                var to0 = ((IHasPosition<double>)de0.To().Data).Position;
                var inside = rectangleMetric.IsEdgeInside(from0, to0);
                Log($"FF: UE {ui} from=({from0.X},{from0.Y}) to=({to0.X},{to0.Y}) inside={inside}");
            }

            void DumpDirectedEdge(int dirIndex)
            {
                var de = d.DirectedEdge(new FixedDirectedEdgeHandle(dirIndex));
                var from = de.From().Handle.Index;
                var to = de.To().Handle.Index;
                var face = de.Face().Handle.Index;
                var prev = de.Prev().Handle.Index;
                var next = de.Next().Handle.Index;
                var rev = de.Rev().Handle.Index;
                Log($"FF: DE {dirIndex} fromV={from} toV={to} face={face} prev={prev} next={next} rev={rev}");
            }

            foreach (var udIndex in new[] { 3, 4 })
            {
                DumpDirectedEdge(udIndex * 2);
                DumpDirectedEdge(udIndex * 2 + 1);
            }
        }

        // Edges: compare exact sets (by undirected edge index)
        var edgesInRect = d.GetEdgesInRectangle(lower, upper).ToList();
        var expectedEdges = d.UndirectedEdges()
            .Where(e =>
            {
                var index = e.Handle.Index;
                var directed = d.DirectedEdge(new FixedDirectedEdgeHandle(index * 2));
                var from = ((IHasPosition<double>)directed.From().Data).Position;
                var to = ((IHasPosition<double>)directed.To().Data).Position;
                return rectangleMetric.IsEdgeInside(from, to);
            })
            .ToList();

        var actualEdgeIndices = edgesInRect.Select(e => e.Handle.Index).OrderBy(i => i).ToList();
        var expectedEdgeIndices = expectedEdges.Select(e => e.Handle.Index).OrderBy(i => i).ToList();

        actualEdgeIndices.Should().Equal(expectedEdgeIndices,
            $"Edge indices must match for regression rectangle lower={lower}, upper={upper}");

        // Vertices: compare exact sets (by fixed vertex handle index)
        var verticesInRect = d.GetVerticesInRectangle(lower, upper).ToList();
        var expectedVertices = d.Vertices()
            .Where(v => rectangleMetric.IsPointInside(((IHasPosition<double>)v.Data).Position))
            .Select(v => v.Handle.Index)
            .OrderBy(i => i)
            .ToList();

        var actualVertexIndices = verticesInRect
            .Select(v => v.Handle.Index)
            .OrderBy(i => i)
            .ToList();

        actualVertexIndices.Should().Equal(expectedVertices,
            $"Vertex indices must match for regression rectangle lower={lower}, upper={upper}");
    }
}
