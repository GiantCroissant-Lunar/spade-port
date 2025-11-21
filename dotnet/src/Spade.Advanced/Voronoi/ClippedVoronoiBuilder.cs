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

        foreach (var face in triangulation.VoronoiFaces())
        {
            var delaunayVertex = face.AsDelaunayVertex();
            var generator = delaunayVertex.Data;

            var rawPolygon = ExtractCellPolygon(face);
            if (rawPolygon.Count < 3)
            {
                continue; // Degenerate or incomplete cell
            }

            var clippedPolygon = ClipPolygonToDomain(rawPolygon, clipVertices);
            if (clippedPolygon.Count < 3)
            {
                continue; // Fully outside domain
            }

            bool isClipped = clippedPolygon.Count != rawPolygon.Count || HasVertexOutsideDomain(rawPolygon, clipVertices);
            resultCells.Add(new ClippedVoronoiCell<V>(generator, clippedPolygon, isClipped));
        }

        return new ClippedVoronoiDiagram<V>(domain, resultCells);
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
