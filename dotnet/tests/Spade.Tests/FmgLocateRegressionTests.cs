using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using FluentAssertions;
using Spade.Primitives;
using Spade;
using Spade.Handles;
using Xunit;

namespace Spade.Tests;

public class FmgLocateRegressionTests
{
    [Fact]
    public void Insert_FmgCapturedPoints_ShouldNotHitLocateInfiniteLoop()
    {
        // Prefer an environment variable so the path can be customized on different machines.
        var pathFromEnv = Environment.GetEnvironmentVariable("SPADE_FMG_FAILURE_CSV");
        var defaultPath = @"d:\lunar-snake\personal-work\yokan-projects\pigeon-pea\dotnet\game-essential\core\src\PigeonPea.Plugin.Map.FMG.Tests\bin\Debug\net9.0\spade_failure_20251122_033428_849.csv";

        var path = !string.IsNullOrWhiteSpace(pathFromEnv) ? pathFromEnv : defaultPath;

        if (!File.Exists(path))
        {
            // If the capture file is not present, we silently skip this regression scenario.
            // This keeps the core Spade test suite self-contained while still enabling
            // targeted debugging when the FMG capture is available.
            return;
        }

        var points = new List<Point2<double>>();

        using (var reader = new StreamReader(path))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("#")) continue;      // Comment / metadata lines
                if (line.StartsWith("x,")) continue;     // Header line
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(',');
                if (parts.Length < 2) continue;

                if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x))
                {
                    continue;
                }
                if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                {
                    continue;
                }

                points.Add(new Point2<double>(x, y));
            }
        }

        points.Count.Should().BeGreaterThan(0, "capture should contain at least one point");

        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            try
            {
                triangulation.Insert(p);

                var validateEnv = Environment.GetEnvironmentVariable("SPADE_VALIDATE_DCEL");
                if (validateEnv == "1")
                {
                    ValidateVertexStars(triangulation);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failure inserting point index {i} of {points.Count} at ({p.X}, {p.Y}): {ex.Message}", ex);
            }
        }

        triangulation.NumVertices.Should().Be(points.Count);
    }

    private static void ValidateVertexStars(DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>> triangulation)
    {
        var maxSteps = triangulation.NumDirectedEdges;

        foreach (var vertex in triangulation.Vertices())
        {
            var outEdge = vertex.OutEdge();
            if (outEdge == null)
            {
                continue;
            }

            var start = outEdge.Value;
            var current = start;
            var visited = new HashSet<int>();
            var steps = 0;

            while (true)
            {
                if (current.From().Handle != vertex.Handle)
                {
                    var diag = Environment.GetEnvironmentVariable("SPADE_DIAG_LOCATE");
                    if (diag == "1")
                    {
                        DumpLocalDcel(triangulation, vertex.Handle.Index, current.Handle.Index);
                    }
                    throw new InvalidOperationException(
                        $"Invalid star around vertex {vertex.Handle.Index}: edge {current.Handle.Index} does not originate from this vertex.");
                }

                if (!visited.Add(current.Handle.Index))
                {
                    var diag = Environment.GetEnvironmentVariable("SPADE_DIAG_LOCATE");
                    if (diag == "1")
                    {
                        DumpLocalDcel(triangulation, vertex.Handle.Index, current.Handle.Index);
                    }
                    throw new InvalidOperationException(
                        $"Cycle in star around vertex {vertex.Handle.Index}: edge {current.Handle.Index} visited twice before returning to start {start.Handle.Index}.");
                }

                steps++;
                if (steps > maxSteps)
                {
                    throw new InvalidOperationException(
                        $"Star walk around vertex {vertex.Handle.Index} exceeded NumDirectedEdges={maxSteps} without closing.");
                }

                var next = current.CCW();
                if (next.Handle.Index == start.Handle.Index)
                {
                    break;
                }

                current = next;
            }
        }
    }

    private static void DumpLocalDcel(
        DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>> triangulation,
        int vertexIndex,
        int edgeIndex)
    {
        var diag = Environment.GetEnvironmentVariable("SPADE_DIAG_LOCATE");
        if (diag != "1")
        {
            return;
        }

        Console.WriteLine("SPADE_DIAG_LOCATE: Local DCEL dump:");
        Console.WriteLine($"  Failing vertex={vertexIndex}, failing edge={edgeIndex}");
        Console.WriteLine($"  NumVertices={triangulation.NumVertices}, NumFaces={triangulation.NumFaces}, NumDirectedEdges={triangulation.NumDirectedEdges}");

        var verticesOfInterest = new HashSet<int> { vertexIndex, 21, 24, 33, 34, 35 };
        foreach (var vIndex in verticesOfInterest)
        {
            if (vIndex < 0 || vIndex >= triangulation.NumVertices)
            {
                continue;
            }

            var vHandle = new FixedVertexHandle(vIndex);
            var vertex = triangulation.Vertex(vHandle);
            var outEdge = vertex.OutEdge();
            Console.WriteLine($"  Vertex {vIndex}: outEdge={(outEdge == null ? "null" : outEdge.Value.Handle.Index.ToString())}");

            if (outEdge == null)
            {
                continue;
            }

            var start = outEdge.Value;
            var current = start;
            var visited = new HashSet<int>();
            var maxSteps = triangulation.NumDirectedEdges;
            var steps = 0;

            while (true)
            {
                var edge = current;
                var from = edge.From().Handle.Index;
                var to = edge.To().Handle.Index;
                var prev = edge.Prev().Handle.Index;
                var next = edge.Next().Handle.Index;
                var face = edge.Face().Handle.Index;
                Console.WriteLine($"    step {steps}: edge={edge.Handle.Index}, from={from}, to={to}, prev={prev}, next={next}, face={face}");

                steps++;
                if (!visited.Add(edge.Handle.Index))
                {
                    Console.WriteLine("    (cycle detected)");
                    break;
                }

                if (steps > maxSteps)
                {
                    Console.WriteLine("    (exceeded maxSteps)");
                    break;
                }

                var ccw = edge.CCW();
                if (ccw.Handle.Index == start.Handle.Index)
                {
                    Console.WriteLine("    (closed ring)");
                    break;
                }

                current = ccw;
            }
        }

        var edgesOfInterest = new HashSet<int> { edgeIndex, 87, 89, 91, 180 };
        foreach (var eIndex in edgesOfInterest)
        {
            if (eIndex < 0 || eIndex >= triangulation.NumDirectedEdges)
            {
                continue;
            }

            var eHandle = new FixedDirectedEdgeHandle(eIndex);
            var edge = triangulation.DirectedEdge(eHandle);
            var from = edge.From().Handle.Index;
            var to = edge.To().Handle.Index;
            var prev = edge.Prev().Handle.Index;
            var next = edge.Next().Handle.Index;
            var face = edge.Face().Handle.Index;
            var rev = edge.Rev();
            var revFrom = rev.From().Handle.Index;
            var revTo = rev.To().Handle.Index;
            var revFace = rev.Face().Handle.Index;

            Console.WriteLine(
                $"  Edge {eIndex}: from={from}, to={to}, prev={prev}, next={next}, face={face}, " +
                $"rev={rev.Handle.Index}, revFrom={revFrom}, revTo={revTo}, revFace={revFace}");
        }
    }
}
