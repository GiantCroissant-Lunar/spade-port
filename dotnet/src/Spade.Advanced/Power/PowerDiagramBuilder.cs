using System;
using System.Collections.Generic;
using Spade.Primitives;
using Spade.Voronoi;

namespace Spade.Advanced.Power;

public static class PowerDiagramBuilder
{
    public static PowerDiagram Build(IReadOnlyList<Point2<double>> points, IReadOnlyList<double> weights)
    {
        if (points is null) throw new ArgumentNullException(nameof(points));
        if (weights is null) throw new ArgumentNullException(nameof(weights));

        if (points.Count != weights.Count)
        {
            throw new ArgumentException("Points and weights must have the same length.", nameof(weights));
        }

        if (points.Count == 0)
        {
            throw new ArgumentException("Points collection must not be empty.", nameof(points));
        }

        var weightedTriangulation = new WeightedDelaunayTriangulation();
        for (var i = 0; i < points.Count; i++)
        {
            weightedTriangulation.Insert(new WeightedPoint(points[i], weights[i]));
        }

        var vertexCount = weightedTriangulation.VertexCount;
        var sites = new List<WeightedPoint>(vertexCount);
        for (var i = 0; i < vertexCount; i++)
        {
            sites.Add(weightedTriangulation.Sites[i]);
        }

        // Extract an approximate polygon per site using the unweighted Voronoi diagram
        // induced by the underlying triangulation. This is a first step toward full
        // power-diagram cell geometry and will be refined in later iterations.
        var triangulation = weightedTriangulation.Triangulation;
        var polygons = new List<Point2<double>>[vertexCount];
        foreach (var face in triangulation.VoronoiFaces())
        {
            var delaunayVertex = face.AsDelaunayVertex();
            var index = delaunayVertex.Handle.Index;
            if (index < 0 || index >= vertexCount)
            {
                continue;
            }

            var polygon = ExtractCellPolygon(face);
            polygons[index] = polygon;
        }

        var neighborSets = weightedTriangulation.BuildNeighborGraph();

        var cells = new List<PowerCell>(sites.Count);
        for (var i = 0; i < sites.Count; i++)
        {
            var neighborSet = i < neighborSets.Count ? neighborSets[i] : new HashSet<int>();
            var neighborList = new List<int>(neighborSet);
            neighborList.Sort();

            var polygon = polygons[i] ?? new List<Point2<double>>();

            // Upgrade from an unweighted Voronoi cell to a power cell by intersecting the
            // initial polygon with the half-spaces defined by neighboring weighted sites.
            // For each neighbor j, we keep the region where π_i(x) <= π_j(x).
            foreach (var neighborIndex in neighborList)
            {
                if (neighborIndex < 0 || neighborIndex >= sites.Count)
                {
                    continue;
                }

                polygon = ClipPolygonToPowerHalfSpace(polygon, sites[i], sites[neighborIndex]);
                if (polygon.Count == 0)
                {
                    break;
                }
            }

            cells.Add(new PowerCell(
                i,
                sites[i],
                polygon,
                neighborList));
        }

        return new PowerDiagram(sites, cells);
    }

    private static List<Point2<double>> ExtractCellPolygon(VoronoiFace<Point2<double>, int, int, int> face)
    {
        var vertices = new List<Point2<double>>();

        foreach (var edge in face.AdjacentEdges())
        {
            var from = edge.From();
            if (from is VoronoiVertex<Point2<double>, int, int, int>.Inner inner && inner.Position.HasValue)
            {
                vertices.Add(inner.Position.Value);
            }
        }

        return vertices;
    }

    private static List<Point2<double>> ClipPolygonToPowerHalfSpace(
        List<Point2<double>> subjectPolygon,
        WeightedPoint keepSite,
        WeightedPoint otherSite)
    {
        var output = new List<Point2<double>>();
        if (subjectPolygon.Count == 0)
        {
            return output;
        }

        const double epsilon = 1e-9;

        // Half-space where power distance to keepSite is less than or equal to that of otherSite:
        // (p_j - p_i) · x <= (|p_j|^2 - w_j - |p_i|^2 + w_i) / 2
        var pi = keepSite.Position;
        var pj = otherSite.Position;

        var nx = pj.X - pi.X;
        var ny = pj.Y - pi.Y;

        double pj2 = pj.X * pj.X + pj.Y * pj.Y;
        double pi2 = pi.X * pi.X + pi.Y * pi.Y;
        double c = 0.5 * (pj2 - otherSite.Weight - pi2 + keepSite.Weight);

        double Eval(Point2<double> p) => nx * p.X + ny * p.Y - c;
        bool Inside(double v) => v <= epsilon;

        var s = subjectPolygon[subjectPolygon.Count - 1];
        var sVal = Eval(s);

        foreach (var e in subjectPolygon)
        {
            var eVal = Eval(e);
            var sInside = Inside(sVal);
            var eInside = Inside(eVal);

            if (eInside)
            {
                if (!sInside && TryIntersectPowerHalfSpace(s, e, sVal, eVal, out var inter1))
                {
                    output.Add(inter1);
                }
                output.Add(e);
            }
            else if (sInside && TryIntersectPowerHalfSpace(s, e, sVal, eVal, out var inter2))
            {
                output.Add(inter2);
            }

            s = e;
            sVal = eVal;
        }

        return output;
    }

    private static bool TryIntersectPowerHalfSpace(
        Point2<double> s,
        Point2<double> e,
        double sVal,
        double eVal,
        out Point2<double> intersection)
    {
        // Linear interpolation of the signed distance values along the segment.
        var denom = eVal - sVal;
        if (Math.Abs(denom) < 1e-12)
        {
            intersection = default;
            return false;
        }

        var t = -sVal / denom;
        if (t < -1e-9 || t > 1 + 1e-9)
        {
            intersection = default;
            return false;
        }

        var x = s.X + t * (e.X - s.X);
        var y = s.Y + t * (e.Y - s.Y);
        intersection = new Point2<double>(x, y);
        return true;
    }
}
