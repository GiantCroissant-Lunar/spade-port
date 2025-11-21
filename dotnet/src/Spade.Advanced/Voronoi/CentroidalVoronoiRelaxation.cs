using System;
using System.Collections.Generic;
using System.Linq;
using Spade;
using Spade.Primitives;

namespace Spade.Advanced.Voronoi;

/// <summary>
/// Helpers for computing centroidal Voronoi relaxations on point sets
/// using the core Spade triangulation and advanced Voronoi utilities.
/// </summary>
public static class CentroidalVoronoiRelaxation
{
    /// <summary>
    /// Applies centroidal Voronoi relaxation to a set of generator points.
    /// Uses Spade's Delaunay triangulation and clipped Voronoi builder under the hood.
    /// </summary>
    /// <param name="points">Initial generator positions.</param>
    /// <param name="domain">
    /// Optional convex clipping domain. If null, a bounding box derived from the points
    /// (with a small margin) is used as the clip polygon.
    /// </param>
    /// <param name="iterations">Number of relaxation iterations (0-10 typical).</param>
    /// <param name="step">
    /// Relaxation step size in [0, 1]. 1.0 moves directly to the centroid, smaller values
    /// move partially toward it (under-relaxation).
    /// </param>
    public static IReadOnlyList<Point2<double>> RelaxPoints(
        IReadOnlyList<Point2<double>> points,
        ClipPolygon? domain = null,
        int iterations = 1,
        double step = 1.0)
    {
        if (points is null) throw new ArgumentNullException(nameof(points));
        if (points.Count == 0 || iterations <= 0)
        {
            return points.ToList();
        }

        iterations = Math.Clamp(iterations, 1, 10);
        step = Math.Clamp(step, 0.0, 1.0);

        var current = points.ToList();

        for (int iter = 0; iter < iterations; iter++)
        {
            if (current.Count == 0)
            {
                break;
            }

            var effectiveDomain = domain ?? BuildBoundingDomain(current);

            // Build triangulation for current generator set
            var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
            foreach (var p in current)
            {
                triangulation.Insert(p);
            }

            var diagram = ClippedVoronoiBuilder.ClipToPolygon(triangulation, effectiveDomain);

            // Map from generator point to index so we can update positions
            var indexByPoint = new Dictionary<Point2<double>, int>(current.Count);
            for (int i = 0; i < current.Count; i++)
            {
                // For this helper we assume exact equality of generator coordinates.
                if (!indexByPoint.ContainsKey(current[i]))
                {
                    indexByPoint[current[i]] = i;
                }
            }

            var next = new List<Point2<double>>(current);

            foreach (var cell in diagram.Cells)
            {
                if (cell.Polygon.Count < 3)
                {
                    continue;
                }

                if (!indexByPoint.TryGetValue(cell.Generator, out var idx))
                {
                    continue;
                }

                var centroid = ComputeCentroid(cell.Polygon);
                var old = current[idx];

                // Under-relaxation: mix old position and centroid.
                var newX = old.X + step * (centroid.X - old.X);
                var newY = old.Y + step * (centroid.Y - old.Y);

                next[idx] = new Point2<double>(newX, newY);
            }

            current = next;
        }

        return current;
    }

    private static ClipPolygon BuildBoundingDomain(IReadOnlyList<Point2<double>> points)
    {
        double minX = points[0].X;
        double maxX = points[0].X;
        double minY = points[0].Y;
        double maxY = points[0].Y;

        for (int i = 1; i < points.Count; i++)
        {
            var p = points[i];
            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;
        }

        var marginX = (maxX - minX) * 0.1 + 1e-3;
        var marginY = (maxY - minY) * 0.1 + 1e-3;

        var ax = minX - marginX;
        var bx = maxX + marginX;
        var ay = minY - marginY;
        var by = maxY + marginY;

        return new ClipPolygon(new[]
        {
            new Point2<double>(ax, ay),
            new Point2<double>(bx, ay),
            new Point2<double>(bx, by),
            new Point2<double>(ax, by),
        });
    }

    private static Point2<double> ComputeCentroid(IReadOnlyList<Point2<double>> polygon)
    {
        double sumX = 0.0;
        double sumY = 0.0;

        for (int i = 0; i < polygon.Count; i++)
        {
            sumX += polygon[i].X;
            sumY += polygon[i].Y;
        }

        var n = polygon.Count;
        return new Point2<double>(sumX / n, sumY / n);
    }
}
