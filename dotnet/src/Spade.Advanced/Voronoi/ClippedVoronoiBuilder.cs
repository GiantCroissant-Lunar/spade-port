using System;
using System.Collections.Generic;
using Spade;
using Spade.Handles;
using Spade.Primitives;
using Spade.Voronoi;

namespace Spade.Advanced.Voronoi;

public static class ClippedVoronoiBuilder
{
    public static ClippedVoronoiDiagram<V> ClipToPolygon<V, DE, UE, F, L>(
        TriangulationBase<V, DE, UE, F, L> triangulation,
        ClipPolygon domain)
        where V : IHasPosition<double>, new()
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        if (triangulation == null) throw new ArgumentNullException(nameof(triangulation));
        if (domain == null) throw new ArgumentNullException(nameof(domain));

        var resultCells = new List<ClippedVoronoiCell<V>>();
        var clipVertices = domain.Vertices;

        // The naive approach of extracting a Voronoi face polygon can yield <3 points for
        // unbounded cells (common when using only a few sites inside a bounded domain),
        // which then gets skipped and creates coverage holes. Instead, compute each site's
        // clipped cell directly as:
        //
        //   Cell(p) ∩ Domain = Domain ∩ ⋂_{q in DelaunayNeighbors(p)} { x | |x-p| <= |x-q| }
        //
        // For Euclidean Voronoi, the neighbor set is exactly the Delaunay adjacency.
        var numVertices = triangulation.NumVertices;
        var generators = new V[numVertices];
        var positions = new Point2<double>[numVertices];

        foreach (var vertex in triangulation.Vertices())
        {
            var idx = vertex.Handle.Index;
            generators[idx] = vertex.Data;
            positions[idx] = vertex.Data.Position;
        }

        var neighbors = BuildNeighborSets(triangulation, numVertices);

        for (var i = 0; i < numVertices; i++)
        {
            var generator = generators[i];
            var site = positions[i];

            var clippedPolygon = new List<Point2<double>>(clipVertices.Count);
            foreach (var v in clipVertices)
            {
                clippedPolygon.Add(v);
            }

            foreach (var neighborIdx in neighbors[i])
            {
                clippedPolygon = ClipPolygonToBisectorHalfPlane(clippedPolygon, site, positions[neighborIdx]);
                if (clippedPolygon.Count < 3)
                {
                    break;
                }
            }

            if (clippedPolygon.Count < 3)
            {
                continue; // Fully outside domain or numerically degenerate.
            }

            bool isClipped = PolygonTouchesDomainBoundary(clippedPolygon, clipVertices);
            resultCells.Add(new ClippedVoronoiCell<V>(generator, clippedPolygon, isClipped));
        }

        return new ClippedVoronoiDiagram<V>(domain, resultCells);
    }

    private static List<int>[] BuildNeighborSets<V, DE, UE, F, L>(
        TriangulationBase<V, DE, UE, F, L> triangulation,
        int numVertices)
        where V : IHasPosition<double>, new()
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        var sets = new HashSet<int>[numVertices];
        for (var i = 0; i < numVertices; i++)
        {
            sets[i] = new HashSet<int>();
        }

        foreach (var edge in triangulation.DirectedEdges())
        {
            var from = edge.From().Handle.Index;
            var to = edge.To().Handle.Index;

            if ((uint)from >= (uint)numVertices || (uint)to >= (uint)numVertices)
            {
                continue;
            }

            if (from == to)
            {
                continue;
            }

            sets[from].Add(to);
            sets[to].Add(from);
        }

        var result = new List<int>[numVertices];
        for (var i = 0; i < numVertices; i++)
        {
            result[i] = new List<int>(sets[i]);
        }

        return result;
    }

    private static List<Point2<double>> ExtractCellPolygon<V, DE, UE, F>(VoronoiFace<V, DE, UE, F> face)
        where V : IHasPosition<double>, new()
        where DE : new()
        where UE : new()
        where F : new()
    {
        var vertices = new List<Point2<double>>();

        foreach (var edge in face.AdjacentEdges())
        {
            var from = edge.From();
            if (from is VoronoiVertex<V, DE, UE, F>.Inner inner && inner.Position.HasValue)
            {
                vertices.Add(inner.Position.Value);
            }
        }

        return vertices;
    }

    private static List<Point2<double>> ClipPolygonToBisectorHalfPlane(
        List<Point2<double>> subjectPolygon,
        Point2<double> site,
        Point2<double> neighbor)
    {
        // Keep points closer to 'site' than to 'neighbor'.
        // Derived from |x-site|^2 <= |x-neighbor|^2 -> x·n <= c
        // where n = (neighbor - site) and c = (|neighbor|^2 - |site|^2)/2
        const double eps = 1e-9;

        if (subjectPolygon.Count == 0)
        {
            return subjectPolygon;
        }

        var nx = neighbor.X - site.X;
        var ny = neighbor.Y - site.Y;
        var c = (neighbor.X * neighbor.X + neighbor.Y * neighbor.Y - site.X * site.X - site.Y * site.Y) / 2.0;

        static double Eval(double x, double y, double nx, double ny, double c) => x * nx + y * ny - c;

        var output = new List<Point2<double>>(subjectPolygon.Count);
        var s = subjectPolygon[subjectPolygon.Count - 1];
        var fs = Eval(s.X, s.Y, nx, ny, c);

        foreach (var e in subjectPolygon)
        {
            var fe = Eval(e.X, e.Y, nx, ny, c);
            var sInside = fs <= eps;
            var eInside = fe <= eps;

            if (eInside)
            {
                if (!sInside && TryIntersectSegmentWithLine(s, e, fs, fe, out var inter1))
                {
                    AddIfNotDuplicate(output, inter1);
                }
                AddIfNotDuplicate(output, e);
            }
            else if (sInside && TryIntersectSegmentWithLine(s, e, fs, fe, out var inter2))
            {
                AddIfNotDuplicate(output, inter2);
            }

            s = e;
            fs = fe;
        }

        return output;
    }

    private static bool TryIntersectSegmentWithLine(
        Point2<double> s,
        Point2<double> e,
        double fs,
        double fe,
        out Point2<double> intersection)
    {
        // Solve fs + t(fe - fs) = 0
        const double eps = 1e-15;
        var denom = fs - fe;
        if (Math.Abs(denom) < eps)
        {
            intersection = default;
            return false;
        }

        var t = fs / denom;
        intersection = new Point2<double>(
            s.X + (e.X - s.X) * t,
            s.Y + (e.Y - s.Y) * t);
        return true;
    }

    private static void AddIfNotDuplicate(List<Point2<double>> list, Point2<double> p)
    {
        if (list.Count == 0)
        {
            list.Add(p);
            return;
        }

        var last = list[list.Count - 1];
        if (Math.Abs(last.X - p.X) < 1e-12 && Math.Abs(last.Y - p.Y) < 1e-12)
        {
            return;
        }

        list.Add(p);
    }

    private static bool PolygonTouchesDomainBoundary(
        IReadOnlyList<Point2<double>> polygon,
        IReadOnlyList<Point2<double>> clipPolygon)
    {
        const double eps = 1e-9;

        int clipCount = clipPolygon.Count;
        foreach (var p in polygon)
        {
            for (int i = 0; i < clipCount; i++)
            {
                var a = clipPolygon[i];
                var b = clipPolygon[(i + 1) % clipCount];
                if (PointOnSegment(p, a, b, eps))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool PointOnSegment(
        Point2<double> p,
        Point2<double> a,
        Point2<double> b,
        double eps)
    {
        var abx = b.X - a.X;
        var aby = b.Y - a.Y;
        var apx = p.X - a.X;
        var apy = p.Y - a.Y;

        var cross = abx * apy - aby * apx;
        var scale = Math.Abs(abx) + Math.Abs(aby) + 1.0;
        if (Math.Abs(cross) > eps * scale)
        {
            return false;
        }

        var dot = apx * abx + apy * aby;
        if (dot < -eps)
        {
            return false;
        }

        var len2 = abx * abx + aby * aby;
        if (dot > len2 + eps)
        {
            return false;
        }

        return true;
    }

    private static List<Point2<double>> ClipPolygonToDomain(
        List<Point2<double>> subjectPolygon,
        IReadOnlyList<Point2<double>> clipPolygon)
    {
        var outputList = new List<Point2<double>>(subjectPolygon);
        if (outputList.Count == 0)
        {
            return outputList;
        }

        int clipCount = clipPolygon.Count;
        for (int i = 0; i < clipCount; i++)
        {
            var clipA = clipPolygon[i];
            var clipB = clipPolygon[(i + 1) % clipCount];

            var inputList = outputList;
            outputList = new List<Point2<double>>();
            if (inputList.Count == 0)
            {
                break;
            }

            var S = inputList[inputList.Count - 1];
            foreach (var E in inputList)
            {
                bool EInside = IsInside(E, clipA, clipB);
                bool SInside = IsInside(S, clipA, clipB);

                if (EInside)
                {
                    if (!SInside && TryComputeIntersection(S, E, clipA, clipB, out var inter1))
                    {
                        outputList.Add(inter1);
                    }
                    outputList.Add(E);
                }
                else if (SInside && TryComputeIntersection(S, E, clipA, clipB, out var inter2))
                {
                    outputList.Add(inter2);
                }

                S = E;
            }
        }

        return outputList;
    }

    private static bool IsInside(Point2<double> p, Point2<double> a, Point2<double> b)
    {
        // Assumes clip polygon is in CCW order; inside is on the left side or on the edge.
        double cross = (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
        return cross >= -1e-9;
    }

    private static bool TryComputeIntersection(
        Point2<double> s,
        Point2<double> e,
        Point2<double> a,
        Point2<double> b,
        out Point2<double> intersection)
    {
        double dx1 = e.X - s.X;
        double dy1 = e.Y - s.Y;
        double dx2 = b.X - a.X;
        double dy2 = b.Y - a.Y;

        double denom = dx1 * dy2 - dy1 * dx2;
        if (Math.Abs(denom) < 1e-12)
        {
            intersection = default;
            return false;
        }

        double dx3 = a.X - s.X;
        double dy3 = a.Y - s.Y;

        double t = (dx3 * dy2 - dy3 * dx2) / denom;
        double u = (dx3 * dy1 - dy3 * dx1) / denom;

        if (t < -1e-9 || t > 1 + 1e-9 || u < -1e-9 || u > 1 + 1e-9)
        {
            intersection = default;
            return false;
        }

        intersection = new Point2<double>(s.X + t * dx1, s.Y + t * dy1);
        return true;
    }

    private static bool HasVertexOutsideDomain(
        List<Point2<double>> polygon,
        IReadOnlyList<Point2<double>> clipPolygon)
    {
        int clipCount = clipPolygon.Count;
        foreach (var p in polygon)
        {
            bool insideAll = true;
            for (int i = 0; i < clipCount; i++)
            {
                var a = clipPolygon[i];
                var b = clipPolygon[(i + 1) % clipCount];
                if (!IsInside(p, a, b))
                {
                    insideAll = false;
                    break;
                }
            }

            if (!insideAll)
            {
                return true;
            }
        }

        return false;
    }
}
