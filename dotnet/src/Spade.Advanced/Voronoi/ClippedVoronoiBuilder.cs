using System;
using System.Collections.Generic;
using Spade;
using Spade.Handles;
using Spade.Primitives;
using Spade.Voronoi;

namespace Spade.Advanced.Voronoi;

public static class ClippedVoronoiBuilder
{
    /// <summary>
    /// Clips a Voronoi diagram to a bounding domain polygon, producing cells in deterministic order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Deterministic Ordering:</b>
    /// </para>
    /// <para>
    /// Generators are processed in ascending index order (0, 1, 2, ...), ensuring that
    /// the resulting cells are ordered by their GeneratorIndex. This allows downstream
    /// consumers to reliably map cells back to their original input site array.
    /// </para>
    /// <para>
    /// <b>Diagnostic Collections:</b>
    /// </para>
    /// <para>
    /// The returned diagram includes diagnostic information about generators that did not
    /// produce valid cells:
    /// <list type="bullet">
    /// <item><see cref="ClippedVoronoiDiagram{TVertex}.DegenerateCells"/>: Generator indices whose cells
    /// became degenerate (fewer than 3 vertices) after clipping</item>
    /// <item><see cref="ClippedVoronoiDiagram{TVertex}.OutsideDomain"/>: Generator indices that lie
    /// entirely outside the clipping domain</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Algorithm:</b>
    /// </para>
    /// <para>
    /// For each generator, the cell is computed as the intersection of the domain polygon
    /// with all half-planes defined by the bisectors between the generator and its Delaunay neighbors:
    /// <c>Cell(p) ∩ Domain = Domain ∩ ⋂_{q in DelaunayNeighbors(p)} { x | |x-p| &lt;= |x-q| }</c>
    /// </para>
    /// </remarks>
    /// <typeparam name="V">The vertex data type.</typeparam>
    /// <typeparam name="DE">The directed edge data type.</typeparam>
    /// <typeparam name="UE">The undirected edge data type.</typeparam>
    /// <typeparam name="F">The face data type.</typeparam>
    /// <typeparam name="L">The hint generator type.</typeparam>
    /// <param name="triangulation">The Delaunay triangulation containing the generator sites.</param>
    /// <param name="domain">The bounding domain polygon to clip cells to.</param>
    /// <returns>A clipped Voronoi diagram with cells ordered by generator index.</returns>
    /// <exception cref="ArgumentNullException">Thrown if triangulation or domain is null.</exception>
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
        var degenerateCells = new List<int>();
        var outsideDomain = new List<int>();
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

        // Process generators in ascending index order (0, 1, 2, ...)
        for (var i = 0; i < numVertices; i++)
        {
            var generator = generators[i];
            var site = positions[i];

            // Check if generator is outside the domain
            if (!IsPointInsidePolygon(site, clipVertices))
            {
                outsideDomain.Add(i);
                continue;
            }

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
                // Cell became degenerate after clipping (fewer than 3 vertices)
                degenerateCells.Add(i);
                continue;
            }

            bool isClipped = PolygonTouchesDomainBoundary(clippedPolygon, clipVertices);
            resultCells.Add(new ClippedVoronoiCell<V>(generator, i, clippedPolygon, isClipped));
        }

        return new ClippedVoronoiDiagram<V>(domain, resultCells, degenerateCells, outsideDomain);
    }

    /// <summary>
    /// Determines whether a point is inside a polygon (assumes CCW winding).
    /// </summary>
    private static bool IsPointInsidePolygon(Point2<double> point, IReadOnlyList<Point2<double>> polygon)
    {
        const double eps = 1e-9;
        int count = polygon.Count;

        for (int i = 0; i < count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % count];

            // Cross product to determine which side of the edge the point is on
            double cross = (b.X - a.X) * (point.Y - a.Y) - (b.Y - a.Y) * (point.X - a.X);

            // For CCW polygon, inside is on the left (cross >= 0)
            // Allow small epsilon for points on the boundary
            if (cross < -eps)
            {
                return false;
            }
        }

        return true;
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

    /// <summary>
    /// Clips a polygon to the half-plane containing points closer to <paramref name="site"/> than to <paramref name="neighbor"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Tie-Breaking Strategy:</b>
    /// </para>
    /// <para>
    /// When a point lies exactly on the bisector line (equidistant from both site and neighbor),
    /// the algorithm uses an epsilon-based tolerance (eps = 1e-9) to determine inclusion.
    /// Points within epsilon of the bisector are considered "inside" the half-plane.
    /// </para>
    /// <para>
    /// This epsilon-based approach provides deterministic tie-breaking because:
    /// <list type="bullet">
    /// <item>The evaluation function <c>Eval(x, y) = x*nx + y*ny - c</c> is computed identically for each point</item>
    /// <item>Points with <c>Eval &lt;= eps</c> are included (inside or on boundary)</item>
    /// <item>Points with <c>Eval &gt; eps</c> are excluded (outside)</item>
    /// </list>
    /// </para>
    /// <para>
    /// For points exactly on the bisector (within floating-point precision), the coordinate values
    /// themselves serve as the implicit tie-breaker since the evaluation is deterministic for any
    /// given (x, y) coordinate pair.
    /// </para>
    /// <para>
    /// <b>Numerical Edge Cases:</b>
    /// </para>
    /// <para>
    /// The algorithm handles near-degenerate cases through:
    /// <list type="bullet">
    /// <item>Epsilon tolerance (1e-9) for point classification to handle floating-point imprecision</item>
    /// <item>Intersection computation uses a tighter epsilon (1e-15) to avoid division by near-zero</item>
    /// <item>Duplicate point filtering (1e-12 tolerance) prevents degenerate polygon edges</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="subjectPolygon">The polygon to clip.</param>
    /// <param name="site">The site (generator) point defining the half-plane.</param>
    /// <param name="neighbor">The neighboring site; the bisector is equidistant from site and neighbor.</param>
    /// <returns>The clipped polygon containing only points closer to site than to neighbor.</returns>
    private static List<Point2<double>> ClipPolygonToBisectorHalfPlane(
        List<Point2<double>> subjectPolygon,
        Point2<double> site,
        Point2<double> neighbor)
    {
        // Keep points closer to 'site' than to 'neighbor'.
        // Derived from |x-site|^2 <= |x-neighbor|^2 -> x·n <= c
        // where n = (neighbor - site) and c = (|neighbor|^2 - |site|^2)/2
        //
        // Tie-breaking: Points with Eval <= eps are considered inside.
        // This provides deterministic behavior for points on or near the bisector.
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

    /// <summary>
    /// Computes the intersection point of a line segment with the bisector line.
    /// </summary>
    /// <remarks>
    /// Uses a tight epsilon (1e-15) to detect near-parallel segments and avoid
    /// division by near-zero denominators. This ensures numerical stability
    /// while maintaining deterministic behavior.
    /// </remarks>
    /// <param name="s">Start point of the segment.</param>
    /// <param name="e">End point of the segment.</param>
    /// <param name="fs">Evaluation of start point against the bisector.</param>
    /// <param name="fe">Evaluation of end point against the bisector.</param>
    /// <param name="intersection">The computed intersection point, if found.</param>
    /// <returns>true if a valid intersection was computed; false if the segment is parallel to the bisector.</returns>
    private static bool TryIntersectSegmentWithLine(
        Point2<double> s,
        Point2<double> e,
        double fs,
        double fe,
        out Point2<double> intersection)
    {
        // Solve fs + t(fe - fs) = 0
        // Uses tight epsilon to avoid division by near-zero while maintaining determinism
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

    /// <summary>
    /// Adds a point to the list if it is not a duplicate of the last point.
    /// </summary>
    /// <remarks>
    /// Uses an epsilon of 1e-12 to detect duplicate points. This prevents
    /// degenerate polygon edges that could arise from floating-point imprecision
    /// during intersection calculations.
    /// </remarks>
    /// <param name="list">The list to add the point to.</param>
    /// <param name="p">The point to add.</param>
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
