using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Spade;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests.Properties;

[Trait("Category", "PropertyTests")]
public class DelaunayEmptyCircumcircleProperties
{
    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    public void EmptyCircumcircle_HoldsForRandomTriangulations(int numPoints)
    {
        for (int trial = 0; trial < 3; trial++)
        {
            var points = GenerateRandomPoints(numPoints, seed: 7000 + trial);
            var triangulation = BuildTriangulation(points);
            var triangles = ExtractTriangles(triangulation, points);

            const double epsilon = 1e-9;

            foreach (var tri in triangles)
            {
                var a = points[tri[0]];
                var b = points[tri[1]];
                var c = points[tri[2]];

                var area2 = OrientedArea2(a, b, c);
                if (Math.Abs(area2) < epsilon)
                {
                    continue;
                }

                var (cx, cy, r2) = ComputeCircumcircle(a, b, c);
                if (double.IsNaN(cx) || double.IsNaN(cy) || double.IsNaN(r2))
                {
                    continue;
                }

                for (int i = 0; i < points.Count; i++)
                {
                    if (i == tri[0] || i == tri[1] || i == tri[2])
                    {
                        continue;
                    }

                    var p = points[i];
                    var dx = p.X - cx;
                    var dy = p.Y - cy;
                    var dist2 = dx * dx + dy * dy;

                    var inside = dist2 < r2 - epsilon;
                    inside.Should().BeFalse($"Point {i} should not lie inside circumcircle of triangle [{tri[0]}, {tri[1]}, {tri[2]}]");
                }
            }
        }
    }

    private static List<Point2<double>> GenerateRandomPoints(int count, int seed)
    {
        var rng = new Random(seed + 3000);
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

    private static List<int[]> ExtractTriangles(
        DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>> triangulation,
        IReadOnlyList<Point2<double>> points)
    {
        var indexByPoint = new Dictionary<(double X, double Y), int>(points.Count);
        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
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
            var indices = new List<int>();
            const int maxIterations = 64;
            var iterations = 0;

            do
            {
                if (iterations++ >= maxIterations)
                {
                    break;
                }

                if (!visited.Add(edge.Handle.Index))
                {
                    break;
                }

                var pos = ((IHasPosition<double>)edge.From().Data).Position;
                if (!indexByPoint.TryGetValue((pos.X, pos.Y), out var idx))
                {
                    break;
                }

                indices.Add(idx);
                edge = edge.Next();
            }
            while (edge.Handle.Index != startIndex);

            if (indices.Count == 3)
            {
                triangles.Add(indices.ToArray());
            }
        }

        return triangles;
    }

    private static double OrientedArea2(Point2<double> a, Point2<double> b, Point2<double> c)
    {
        var abx = b.X - a.X;
        var aby = b.Y - a.Y;
        var acx = c.X - a.X;
        var acy = c.Y - a.Y;
        return abx * acy - aby * acx;
    }

    private static (double Cx, double Cy, double Radius2) ComputeCircumcircle(
        Point2<double> a,
        Point2<double> b,
        Point2<double> c)
    {
        var ax = a.X;
        var ay = a.Y;
        var bx = b.X;
        var by = b.Y;
        var cx = c.X;
        var cy = c.Y;

        var aSq = ax * ax + ay * ay;
        var bSq = bx * bx + by * by;
        var cSq = cx * cx + cy * cy;

        var d = 2.0 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
        if (Math.Abs(d) < 1e-12)
        {
            return (double.NaN, double.NaN, double.NaN);
        }

        var ux = (aSq * (by - cy) + bSq * (cy - ay) + cSq * (ay - by)) / d;
        var uy = (aSq * (cx - bx) + bSq * (ax - cx) + cSq * (bx - ax)) / d;

        var dx = ax - ux;
        var dy = ay - uy;
        var r2 = dx * dx + dy * dy;

        return (ux, uy, r2);
    }
}
