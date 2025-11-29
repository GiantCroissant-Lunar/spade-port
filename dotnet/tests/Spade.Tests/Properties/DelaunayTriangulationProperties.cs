using Xunit;
using Spade;
using Spade.Primitives;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Spade.Tests.Properties;

/// <summary>
/// Property-based-style tests for Delaunay triangulation invariants.
/// Using deterministic test cases rather than full FsCheck for now (simpler integration).
/// </summary>
[Trait("Category", "PropertyTests")]
public class DelaunayTriangulationProperties
{
    private static Random _random = new Random(42); // Deterministic seed

    /// <summary>
    /// Euler's formula for planar graphs: V - E + F = 2
    /// </summary>
    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    public void EulerCharacteristic_HoldsForRandomTriangulations(int numPoints)
    {
        for (int trial = 0; trial < 5; trial++)
        {
            var points = GenerateRandomPoints(numPoints, trial);
            var triangulation = BuildTriangulation(points);

            var V = triangulation.NumVertices;
            var E = triangulation.NumUndirectedEdges;
            var F = triangulation.NumFaces;

            // Euler's formula: V - E + F = 2
            var eulerValue = V - E + F;
            eulerValue.Should().Be(2, $"trial {trial}: V={V}, E={E}, F={F}");
        }
    }

    /// <summary>
    /// Every directed edge must have a valid twin.
    /// </summary>
    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public void AllEdges_HaveValidTwins(int numPoints)
    {
        for (int trial = 0; trial < 5; trial++)
        {
            var points = GenerateRandomPoints(numPoints, trial);
            var triangulation = BuildTriangulation(points);

            foreach (var edge in triangulation.DirectedEdges())
            {
                var twin = edge.Rev();
                var twinTwin = twin.Rev();

                twinTwin.Handle.Index.Should().Be(edge.Handle.Index,
                    $"Edge {edge.Handle.Index} twin integrity failed");
            }
        }
    }

    /// <summary>
    /// All inner faces must be triangular (3 vertices).
    /// </summary>
    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public void AllInnerFaces_AreTriangular(int numPoints)
    {
        for (int trial = 0; trial < 5; trial++)
        {
            var points = GenerateRandomPoints(numPoints, trial);
            var triangulation = BuildTriangulation(points);

            foreach (var face in triangulation.InnerFaces())
            {
                var vertexCount = CountFaceVertices(face);
                vertexCount.Should().Be(3, $"Face {face.Handle.Index} should be triangular");
            }
        }
    }

    /// <summary>
    /// Triangulation should be connected.
    /// </summary>
    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public void Triangulation_IsConnected(int numPoints)
    {
        for (int trial = 0; trial < 5; trial++)
        {
            var points = GenerateRandomPoints(numPoints, trial);
            var triangulation = BuildTriangulation(points);

            if (triangulation.NumVertices == 0) continue;

            // BFS from first vertex
            var visited = new HashSet<int>();
            var queue = new Queue<int>();

            var firstVertex = triangulation.Vertices().First();
            queue.Enqueue(firstVertex.Handle.Index);
            visited.Add(firstVertex.Handle.Index);

            while (queue.Count > 0)
            {
                var currentIdx = queue.Dequeue();
                var vertex = triangulation.Vertex(new Spade.Handles.FixedVertexHandle(currentIdx));

                var outEdge = vertex.OutEdge();
                if (outEdge == null) continue;

                var edge = outEdge.Value;
                var start = edge.Handle.Index;
                var maxIter = 1000;
                var iter = 0;

                do
                {
                    if (iter++ > maxIter) break;

                    var neighbor = edge.To();
                    if (visited.Add(neighbor.Handle.Index))
                    {
                        queue.Enqueue(neighbor.Handle.Index);
                    }

                    edge = edge.CCW();
                } while (edge.Handle.Index != start);
            }

            visited.Count.Should().Be(triangulation.NumVertices,
                $"All {triangulation.NumVertices} vertices should be reachable");
        }
    }

    /// <summary>
    /// DCEL next/prev consistency: next().prev() == self
    /// </summary>
    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    public void Edge_NextPrev_Consistency(int numPoints)
    {
        for (int trial = 0; trial < 5; trial++)
        {
            var points = GenerateRandomPoints(numPoints, trial);
            var triangulation = BuildTriangulation(points);

            foreach (var edge in triangulation.DirectedEdges())
            {
                var next = edge.Next();
                var prev = edge.Prev();

                next.Prev().Handle.Index.Should().Be(edge.Handle.Index, "next.prev == self");
                prev.Next().Handle.Index.Should().Be(edge.Handle.Index, "prev.next == self");
            }
        }
    }

    // Helper methods

    private static List<Point2<double>> GenerateRandomPoints(int count, int seed)
    {
        var rng = new Random(seed + 1000);
        var points = new List<Point2<double>>();
        var seen = new HashSet<(double, double)>();

        while (points.Count < count)
        {
            var x = (rng.NextDouble() - 0.5) * 200; // Range: -100 to 100
            var y = (rng.NextDouble() - 0.5) * 200;

            // Round to avoid floating point duplicates
            x = Math.Round(x, 6);
            y = Math.Round(y, 6);

            if (seen.Add((x, y)))
            {
                points.Add(new Point2<double>(x, y));
            }
        }

        return points;
    }

    private static DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>
        BuildTriangulation(List<Point2<double>> points)
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        foreach (var p in points)
        {
            triangulation.Insert(p);
        }
        return triangulation;
    }

    private static int CountFaceVertices(Spade.Handles.FaceHandle<Point2<double>, int, int, int> face)
    {
        var edge = face.AdjacentEdge();
        if (edge == null) return 0;

        int count = 0;
        var start = edge.Value.Handle.Index;
        var current = edge.Value;
        var maxIter = 100;

        do
        {
            count++;
            current = current.Next();
            if (--maxIter == 0) break;
        } while (current.Handle.Index != start);

        return count;
    }
}
