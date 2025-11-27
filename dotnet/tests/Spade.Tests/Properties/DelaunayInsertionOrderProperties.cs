using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Spade;
using Spade.Primitives;
using Xunit;
using Spade.Tests.Validation;

namespace Spade.Tests.Properties;

[Trait("Category", "PropertyTests")]
public class DelaunayInsertionOrderProperties
{
    [Theory]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(50)]
    public void Triangulation_IsIndependentOfInsertionOrder(int numPoints)
    {
        var basePoints = GenerateRandomPoints(numPoints, seed: 1234);

        var baseline = BuildTriangulation(basePoints);
        var baselineOracle = ExtractCanonicalOracle(baseline, basePoints);

        for (int trial = 0; trial < 5; trial++)
        {
            var permuted = Permute(basePoints, trialSeed: 1000 + trial);
            var triangulation = BuildTriangulation(permuted);
            var oracle = ExtractCanonicalOracle(triangulation, basePoints);

            oracle.Triangles.Should().BeEquivalentTo(
                baselineOracle.Triangles,
                "Delaunay triangulation should not depend on insertion order");
        }
    }

    private static List<Point2<double>> GenerateRandomPoints(int count, int seed)
    {
        var rng = new Random(seed + 1000);
        var points = new List<Point2<double>>();
        var seen = new HashSet<(double, double)>();

        while (points.Count < count)
        {
            var x = (rng.NextDouble() - 0.5) * 200;
            var y = (rng.NextDouble() - 0.5) * 200;

            x = Math.Round(x, 6);
            y = Math.Round(y, 6);

            if (seen.Add((x, y)))
            {
                points.Add(new Point2<double>(x, y));
            }
        }

        return points;
    }

    private static IReadOnlyList<Point2<double>> Permute(IReadOnlyList<Point2<double>> points, int trialSeed)
    {
        var rng = new Random(trialSeed);
        var list = points.ToList();

        for (int i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }

    private static DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>
        BuildTriangulation(IEnumerable<Point2<double>> points)
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        foreach (var p in points)
        {
            triangulation.Insert(p);
        }

        return triangulation;
    }

    private sealed class OraclePointComparer : IEqualityComparer<(double X, double Y)>
    {
        private readonly double _epsilon;

        public OraclePointComparer(double epsilon)
        {
            _epsilon = epsilon;
        }

        public bool Equals((double X, double Y) a, (double X, double Y) b)
        {
            return Math.Abs(a.X - b.X) <= _epsilon && Math.Abs(a.Y - b.Y) <= _epsilon;
        }

        public int GetHashCode((double X, double Y) p)
        {
            var rx = Math.Round(p.X / _epsilon);
            var ry = Math.Round(p.Y / _epsilon);
            return HashCode.Combine(rx, ry);
        }
    }

    private static OracleTriangulationOutput ExtractCanonicalOracle(
        DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>> triangulation,
        IReadOnlyList<Point2<double>> referencePoints)
    {
        var comparer = new OraclePointComparer(1e-9);
        var indexByPoint = new Dictionary<(double X, double Y), int>(referencePoints.Count, comparer);
        for (int i = 0; i < referencePoints.Count; i++)
        {
            var p = referencePoints[i];
            indexByPoint[(p.X, p.Y)] = i;
        }

        var triangles = new List<int[]>();

        foreach (var face in triangulation.InnerFaces())
        {
            var edgeOpt = face.AdjacentEdge();
            if (edgeOpt is null)
            {
                continue;
            }

            var edge = edgeOpt.Value;
            var startIndex = edge.Handle.Index;
            var visited = new HashSet<int>();
            var vertices = new List<int>();
            const int maxIterations = 64;
            var iterations = 0;

            do
            {
                if (iterations++ >= maxIterations)
                {
                    throw new InvalidOperationException($"Exceeded max iterations while walking face {face.Handle.Index}.");
                }

                if (!visited.Add(edge.Handle.Index))
                {
                    throw new InvalidOperationException($"Cycle detected while walking face {face.Handle.Index}.");
                }

                var pos = ((IHasPosition<double>)edge.From().Data).Position;
                if (!indexByPoint.TryGetValue((pos.X, pos.Y), out var idx))
                {
                    throw new InvalidOperationException($"Vertex at ({pos.X}, {pos.Y}) not found in referencePoints.");
                }

                vertices.Add(idx);
                edge = edge.Next();
            }
            while (edge.Handle.Index != startIndex);

            if (vertices.Count != 3)
            {
                throw new InvalidOperationException($"Expected triangular face, but found {vertices.Count} vertices.");
            }

            vertices.Sort();
            triangles.Add(vertices.ToArray());
        }

        var normalized = triangles
            .Select(t =>
            {
                var copy = (int[])t.Clone();
                Array.Sort(copy);
                return copy;
            })
            .OrderBy(t => t[0]).ThenBy(t => t[1]).ThenBy(t => t[2])
            .ToList();

        var oraclePoints = referencePoints
            .Select(p => new OraclePoint(p.X, p.Y))
            .ToList();

        return new OracleTriangulationOutput(oraclePoints, normalized);
    }
}
