using System;
using System.Collections.Generic;
using System.Diagnostics;
using Spade.DCEL;
using Spade.Handles;
using Spade.Primitives;

namespace Spade.Refinement;

/// <summary>
/// Extension methods for performing basic mesh refinement on constrained
/// Delaunay triangulations.
///
/// This initial implementation focuses on a simple area-based refinement
/// strategy: while there exists an inner triangle whose area exceeds the
/// specified maximum, insert a Steiner point at its circumcenter.
/// </summary>
public static class MeshRefinementExtensions
{
    private enum RefinementHint
    {
        Ignore,
        ShouldRefine,
        MustRefine
    }

    private const double RefinementMarginFactor = 1000.0;

    public static RefinementResult Refine<DE, UE, F, L>(
        this ConstrainedDelaunayTriangulation<Point2<double>, DE, UE, F, L> triangulation,
        RefinementParameters parameters)
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        if (triangulation == null) throw new ArgumentNullException(nameof(triangulation));
        if (parameters == null) throw new ArgumentNullException(nameof(parameters));

        var maxAreaOpt = parameters.MaxAllowedArea;
        bool hasAreaConstraint = maxAreaOpt.HasValue && maxAreaOpt.Value > 0.0;
        double maxArea = hasAreaConstraint ? maxAreaOpt!.Value : 0.0;

        var minAngleLimitRad = parameters.AngleLimit.Radians;
        var angleLimitRatio = parameters.AngleLimit.RadiusToShortestEdgeLimit;
        bool hasAngleConstraint = parameters.EnableAngleRefinement && minAngleLimitRad > 0.0;

        if (!hasAreaConstraint && !hasAngleConstraint)
        {
            // Nothing to refine against yet: neither area nor angle constraints
            // are active, so refinement is a no-op.
            return new RefinementResult(0, false);
        }

        int added = 0;
        bool limited = false;

        const double areaEpsilon = 1e-12;
        const double angleEpsilon = 1e-6;

        var diagEnv = Environment.GetEnvironmentVariable("SPADE_DIAG_REFINE");
        var diag = diagEnv == "1";

        var ignoredFaces = new HashSet<int>();
        var (minX, maxX, minY, maxY) = ComputeBoundingBox(triangulation);

        while (true)
        {
            FaceHandle<Point2<double>, DE, CdtEdge<UE>, F>? worstFace = null;
            double bestScore = 0.0;

            foreach (var face in triangulation.InnerFaces())
            {
                if (ignoredFaces.Contains(face.Handle.Index))
                {
                    continue;
                }

                var hint = GetRefinementHint(triangulation, face, parameters, hasAreaConstraint, maxArea, hasAngleConstraint, angleLimitRatio);
                if (hint == RefinementHint.Ignore)
                {
                    continue;
                }

                double score = 0.0;

                if (hasAreaConstraint)
                {
                    var area = ComputeTriangleArea(triangulation, face);
                    if (area > maxArea + areaEpsilon)
                    {
                        var areaScore = area / maxArea;
                        if (areaScore > score) score = areaScore;
                    }
                }

                if (hasAngleConstraint)
                {
                    var ratio = ComputeRadiusToShortestEdgeRatio(triangulation, face);
                    if (!double.IsPositiveInfinity(ratio))
                    {
                        var limit = angleLimitRatio;
                        if (ratio > limit + angleEpsilon)
                        {
                            var angleScore = ratio / limit;
                            if (angleScore > score) score = angleScore;
                        }
                    }
                }

                if (hint == RefinementHint.MustRefine)
                {
                    score += 10.0;
                }
                else if (hint == RefinementHint.ShouldRefine)
                {
                    score += 1.0;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    worstFace = face;
                }
            }

            if (diag)
            {
                var areas = new List<(FaceHandle<Point2<double>, DE, CdtEdge<UE>, F> Face, double Area)>();
                foreach (var face in triangulation.InnerFaces())
                {
                    areas.Add((face, ComputeTriangleArea(triangulation, face)));
                }
                var maxAreaInLoop = areas.Count > 0 ? areas.Max(t => t.Area) : 0.0;

                var lines = new List<string>
                {
                    $"SPADE_DIAG_REFINE: added={added}, bestScore={bestScore}, maxArea={maxAreaInLoop}, numFaces={areas.Count}"
                };

                if (added <= 1)
                {
                    foreach (var (face, area) in areas.OrderByDescending(t => t.Area))
                    {
                        var e = face.AdjacentEdge();
                        if (e == null) continue;
                        var v0 = e.Value.From().Data.Position;
                        var v1 = e.Value.To().Data.Position;
                        var v2 = e.Value.Next().To().Data.Position;
                        lines.Add(
                            $"  FACE idx={face.Handle.Index}, area={area}, v0=({v0.X},{v0.Y}), v1=({v1.X},{v1.Y}), v2=({v2.X},{v2.Y})");
                    }
                }

                try
                {
                    System.IO.File.AppendAllLines("refine_diag.txt", lines);
                }
                catch
                {
                    // Diagnostics only – ignore IO failures
                }
            }

            if (worstFace is null || bestScore <= 0.0)
            {
                break;
            }

            if (added >= parameters.MaxAdditionalVertices)
            {
                limited = true;
                break;
            }

            var splitPoint = hasAngleConstraint
                ? worstFace.Value.Circumcenter()
                : ComputeTriangleBarycenter(triangulation, worstFace.Value);

            double width = maxX - minX;
            double height = maxY - minY;
            double maxDim = Math.Max(width, height);
            if (maxDim == 0) maxDim = 1.0;
            double margin = maxDim * RefinementMarginFactor;

            if (splitPoint.X < minX - margin || splitPoint.X > maxX + margin ||
                splitPoint.Y < minY - margin || splitPoint.Y > maxY + margin)
            {
                if (diag)
                {
                    var lines = new List<string>
                    {
                        $"SPADE_DIAG_REFINE: Skipping face {worstFace.Value.Handle.Index} due to extreme split point ({splitPoint.X}, {splitPoint.Y})"
                    };
                    try
                    {
                        System.IO.File.AppendAllLines("refine_diag.txt", lines);
                    }
                    catch
                    {
                        // Diagnostics only – ignore IO failures
                    }
                }
                ignoredFaces.Add(worstFace.Value.Handle.Index);
                continue;
            }

            if (parameters.KeepConstraintEdges && triangulation.NumConstraints > 0)
            {
                if (TryGetEncroachedConstraintEdge(triangulation, splitPoint, out var encroachedEdge))
                {
                    // Splitting an encroached constraint edge should not count
                    // towards AddedVertices, matching the test expectations that
                    // only pure refinement insertions are counted.
                    ResolveEncroachment(
                        triangulation,
                        new Queue<FixedUndirectedEdgeHandle>(),
                        new Queue<FaceHandle<Point2<double>, DE, CdtEdge<UE>, F>>(),
                        encroachedEdge);
                    continue;
                }
            }

            triangulation.Insert(new Point2<double>(splitPoint.X, splitPoint.Y));
            added++;

            if (splitPoint.X < minX) minX = splitPoint.X;
            if (splitPoint.X > maxX) maxX = splitPoint.X;
            if (splitPoint.Y < minY) minY = splitPoint.Y;
            if (splitPoint.Y > maxY) maxY = splitPoint.Y;
        }

        // After refinement, mark finite outer faces (those that lie in the
        // exterior region w.r.t. constraints) so that subsequent calls to
        // InnerFaces() only see true inner faces.
        MarkOuterFacesUsingRefinementTopology(triangulation);

        return new RefinementResult(added, limited);
    }

    private static void MarkOuterFacesUsingRefinementTopology<DE, UE, F, L>(
        ConstrainedDelaunayTriangulation<Point2<double>, DE, UE, F, L> triangulation)
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        var dcel = triangulation._dcel;

        // Reset all non-structural faces to Inner.
        for (int i = 1; i < dcel.Faces.Count; i++)
        {
            var entry = dcel.Faces[i];
            entry.Kind = FaceKind.Inner;
            dcel.Faces[i] = entry;
        }

        // Compute outer faces using a port of spade's calculate_outer_faces
        // and tag those faces as Outer in the DCEL.
        var outerFaceIndices = CalculateOuterFaceIndices(triangulation);
        foreach (var index in outerFaceIndices)
        {
            if (index <= 0 || index >= dcel.Faces.Count)
            {
                continue;
            }

            var entry = dcel.Faces[index];
            entry.Kind = FaceKind.Outer;
            dcel.Faces[index] = entry;
        }

        AssertFaceKindsConsistent(dcel, outerFaceIndices);
    }

    /// <summary>
    /// Port of spade's calculate_outer_faces: identifies finite faces that lie
    /// in the exterior region with respect to constraint edges by "peeling"
    /// layers starting from the convex hull.
    /// </summary>
    private static HashSet<int> CalculateOuterFaceIndices<DE, UE, F, L>(
        ConstrainedDelaunayTriangulation<Point2<double>, DE, UE, F, L> triangulation)
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        var resultOuterFaces = new HashSet<int>();

        // Degenerate case: line-only triangulations have no finite outer faces.
        if (triangulation.NumFaces <= 1)
        {
            return resultOuterFaces;
        }

        var innerFaces = new HashSet<int>();
        var outerFaces = new HashSet<int>();

        // Approximate spade's convex_hull(): collect one directed edge per
        // undirected hull edge, oriented so that the triangulation interior
        // is on the left side, then follow Rust's .map(|edge| edge.rev()).
        var convexHullEdges = GetConvexHullEdges(triangulation);

        var currentTodoList = new List<FixedDirectedEdgeHandle>(convexHullEdges.Count);
        var seenStarts = new HashSet<int>();
        foreach (var edge in convexHullEdges)
        {
            var rev = edge.Rev();
            if (seenStarts.Add(rev.Handle.Index))
            {
                currentTodoList.Add(rev.Handle);
            }
        }

        var nextTodoList = new List<FixedDirectedEdgeHandle>();
        var returnOuterFaces = true;

        while (true)
        {
            // Peel off one "layer" of faces.
            while (currentTodoList.Count > 0)
            {
                var nextHandle = currentTodoList[^1];
                currentTodoList.RemoveAt(currentTodoList.Count - 1);

                var edge = triangulation.DirectedEdge(nextHandle);

                List<FixedDirectedEdgeHandle> list;
                HashSet<int> faceSet;

                if (IsConstraintEdge(triangulation, edge))
                {
                    // Crossing a constraint edge: this face belongs to the
                    // *next* layer.
                    list = nextTodoList;
                    faceSet = innerFaces;
                }
                else
                {
                    // Free edge: this face belongs to the current outer layer.
                    list = currentTodoList;
                    faceSet = outerFaces;
                }

                var face = edge.Face();
                if (face.IsOuter)
                {
                    continue;
                }

                var index = face.Handle.Index;
                if (faceSet.Add(index))
                {
                    list.Add(edge.Prev().Rev().Handle);
                    list.Add(edge.Next().Rev().Handle);
                }
            }

            if (nextTodoList.Count == 0)
            {
                break;
            }

            // Swap inner/outer sets and advance to the next layer, mirroring
            // the behavior of spade's calculate_outer_faces.
            (innerFaces, outerFaces) = (outerFaces, innerFaces);
            (nextTodoList, currentTodoList) = (currentTodoList, nextTodoList);

            returnOuterFaces = !returnOuterFaces;
        }

        resultOuterFaces = returnOuterFaces ? outerFaces : innerFaces;
        return resultOuterFaces;
    }

    [Conditional("DEBUG")]
    private static void AssertFaceKindsConsistent<DE, UE, F>(
        Dcel<Point2<double>, DE, CdtEdge<UE>, F> dcel,
        HashSet<int> outerFaceIndices)
        where DE : new()
        where UE : new()
        where F : new()
    {
        if (dcel.Faces.Count == 0)
        {
            throw new InvalidOperationException("DCEL must contain at least the structural outer face.");
        }

        if (dcel.Faces[0].Kind != FaceKind.Outer)
        {
            throw new InvalidOperationException("Structural outer face (index 0) must be tagged as FaceKind.Outer.");
        }

        for (int i = 1; i < dcel.Faces.Count; i++)
        {
            var kind = dcel.Faces[i].Kind;
            var shouldBeOuter = outerFaceIndices.Contains(i);

            if (shouldBeOuter && kind != FaceKind.Outer)
            {
                throw new InvalidOperationException($"Face {i} should be tagged as Outer but is {kind}.");
            }

            if (!shouldBeOuter && kind != FaceKind.Inner)
            {
                throw new InvalidOperationException($"Face {i} should be tagged as Inner but is {kind}.");
            }
        }
    }

    private static List<DirectedEdgeHandle<Point2<double>, DE, CdtEdge<UE>, F>> GetConvexHullEdges<DE, UE, F, L>(
        ConstrainedDelaunayTriangulation<Point2<double>, DE, UE, F, L> triangulation)
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        var result = new List<DirectedEdgeHandle<Point2<double>, DE, CdtEdge<UE>, F>>();
        var seenUndirected = new HashSet<int>();

        foreach (var edge in triangulation.DirectedEdges())
        {
            var face = edge.Face();
            var revFace = edge.Rev().Face();

            // Hull edges separate the structural outer face (index 0) from a finite face.
            if (face.IsOuter == revFace.IsOuter)
            {
                continue;
            }

            var undirectedIndex = edge.AsUndirected().Handle.Index;
            if (!seenUndirected.Add(undirectedIndex))
            {
                continue;
            }

            // Interior edge orientation: face on the left is not the structural outer face.
            var interior = face.IsOuter ? edge.Rev() : edge;
            result.Add(interior);
        }

        return result;
    }

    private static bool IsConstraintEdge<DE, UE, F, L>(
        ConstrainedDelaunayTriangulation<Point2<double>, DE, UE, F, L> triangulation,
        DirectedEdgeHandle<Point2<double>, DE, CdtEdge<UE>, F> edge)
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        var dcel = triangulation._dcel;
        var undirectedIndex = edge.AsUndirected().Handle.Index;
        return dcel.Edges[undirectedIndex].UndirectedData.IsConstraintEdge;
    }

    private static double ComputeTriangleArea<DE, UE, F, L>(
        ConstrainedDelaunayTriangulation<Point2<double>, DE, UE, F, L> triangulation,
        FaceHandle<Point2<double>, DE, CdtEdge<UE>, F> face)
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        var edge = face.AdjacentEdge() ?? throw new InvalidOperationException("Face has no adjacent edge");

        var v0 = edge.From().Data.Position;
        var v1 = edge.To().Data.Position;
        var v2 = edge.Next().To().Data.Position;

        var ax = v1.X - v0.X;
        var ay = v1.Y - v0.Y;
        var bx = v2.X - v0.X;
        var by = v2.Y - v0.Y;

        var cross = ax * by - ay * bx;
        return Math.Abs(cross) * 0.5;
    }

    private static double ComputeTriangleMinAngle<DE, UE, F, L>(
        ConstrainedDelaunayTriangulation<Point2<double>, DE, UE, F, L> triangulation,
        FaceHandle<Point2<double>, DE, CdtEdge<UE>, F> face)
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        var edge = face.AdjacentEdge() ?? throw new InvalidOperationException("Face has no adjacent edge");

        var v0 = edge.From().Data.Position;
        var v1 = edge.To().Data.Position;
        var v2 = edge.Next().To().Data.Position;

        static double AngleAt(Point2<double> a, Point2<double> b, Point2<double> c)
        {
            var v1x = a.X - b.X;
            var v1y = a.Y - b.Y;
            var v2x = c.X - b.X;
            var v2y = c.Y - b.Y;

            var dot = v1x * v2x + v1y * v2y;
            var len1Sq = v1x * v1x + v1y * v1y;
            var len2Sq = v2x * v2x + v2y * v2y;

            var denom = Math.Sqrt(len1Sq * len2Sq);
            if (denom < 1e-12)
            {
                return 0.0;
            }

            var cos = dot / denom;
            cos = Math.Clamp(cos, -1.0, 1.0);
            return Math.Acos(cos);
        }

        var a0 = AngleAt(v1, v0, v2);
        var a1 = AngleAt(v0, v1, v2);
        var a2 = AngleAt(v0, v2, v1);

        return Math.Min(a0, Math.Min(a1, a2));
    }

    private static Point2<double> ComputeTriangleBarycenter<DE, UE, F, L>(
        ConstrainedDelaunayTriangulation<Point2<double>, DE, UE, F, L> triangulation,
        FaceHandle<Point2<double>, DE, CdtEdge<UE>, F> face)
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        var edge = face.AdjacentEdge() ?? throw new InvalidOperationException("Face has no adjacent edge");

        var v0 = edge.From().Data.Position;
        var v1 = edge.To().Data.Position;
        var v2 = edge.Next().To().Data.Position;

        return new Point2<double>(
            (v0.X + v1.X + v2.X) / 3.0,
            (v0.Y + v1.Y + v2.Y) / 3.0);
    }

    private static RefinementHint GetRefinementHint<DE, UE, F, L>(
        ConstrainedDelaunayTriangulation<Point2<double>, DE, UE, F, L> triangulation,
        FaceHandle<Point2<double>, DE, CdtEdge<UE>, F> face,
        RefinementParameters parameters,
        bool hasAreaConstraint,
        double maxArea,
        bool hasAngleConstraint,
        double angleLimitRatio)
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        double? cachedArea = null;

        if (hasAreaConstraint)
        {
            var area = ComputeTriangleArea(triangulation, face);
            cachedArea = area;
            if (area > maxArea)
            {
                return RefinementHint.MustRefine;
            }
        }

        if (parameters.MinRequiredArea.HasValue)
        {
            var minArea = parameters.MinRequiredArea.Value;
            var area = cachedArea ?? ComputeTriangleArea(triangulation, face);
            if (area < minArea)
            {
                return RefinementHint.Ignore;
            }
        }

        if (hasAngleConstraint)
        {
            var ratio = ComputeRadiusToShortestEdgeRatio(triangulation, face);
            if (!double.IsPositiveInfinity(ratio) && ratio > angleLimitRatio)
            {
                return RefinementHint.ShouldRefine;
            }
        }

        return RefinementHint.Ignore;
    }

    private static void ResolveEncroachment<DE, UE, F, L>(
        ConstrainedDelaunayTriangulation<Point2<double>, DE, UE, F, L> triangulation,
        Queue<FixedUndirectedEdgeHandle> encroachedSegmentsBuffer,
        Queue<FaceHandle<Point2<double>, DE, CdtEdge<UE>, F>> encroachedFacesBuffer,
        FixedUndirectedEdgeHandle encroachedEdge)
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        _ = encroachedSegmentsBuffer;
        _ = encroachedFacesBuffer;

        var directed = new FixedDirectedEdgeHandle(encroachedEdge.Index * 2);
        var edge = triangulation.DirectedEdge(directed);

        var from = edge.From().Data.Position;
        var to = edge.To().Data.Position;

        if (from.X == to.X && from.Y == to.Y)
        {
            return;
        }

        var mid = new Point2<double>(
            (from.X + to.X) * 0.5,
            (from.Y + to.Y) * 0.5);

        mid = MathUtils.MitigateUnderflow(mid);

        MathUtils.ValidateCoordinate(mid.X);
        MathUtils.ValidateCoordinate(mid.Y);

        triangulation.Insert(mid);
    }

    private static double ComputeRadiusToShortestEdgeRatio<DE, UE, F, L>(
        ConstrainedDelaunayTriangulation<Point2<double>, DE, UE, F, L> triangulation,
        FaceHandle<Point2<double>, DE, CdtEdge<UE>, F> face)
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        var edge = face.AdjacentEdge() ?? throw new InvalidOperationException("Face has no adjacent edge");

        var v0 = edge.From().Data.Position;
        var v1 = edge.To().Data.Position;
        var v2 = edge.Next().To().Data.Position;

        static double EdgeLengthSquared(Point2<double> a, Point2<double> b)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            return dx * dx + dy * dy;
        }

        var e01 = EdgeLengthSquared(v0, v1);
        var e12 = EdgeLengthSquared(v1, v2);
        var e20 = EdgeLengthSquared(v2, v0);

        var shortest2 = Math.Min(e01, Math.Min(e12, e20));
        if (shortest2 <= 0.0)
        {
            return double.PositiveInfinity;
        }

        var center = MathUtils.Circumcenter(v0, v1, v2);
        var rdx = v0.X - center.X;
        var rdy = v0.Y - center.Y;
        var radius2 = rdx * rdx + rdy * rdy;
        if (radius2 <= 0.0)
        {
            return 0.0;
        }

        var shortest = Math.Sqrt(shortest2);
        var radius = Math.Sqrt(radius2);
        return radius / shortest;
    }

    private static bool IsFixedEdge<DE, UE, F, L>(
        ConstrainedDelaunayTriangulation<Point2<double>, DE, UE, F, L> triangulation,
        UndirectedEdgeHandle<Point2<double>, DE, CdtEdge<UE>, F> edge)
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        if (edge.Data.IsConstraintEdge)
        {
            return true;
        }

        var fixedUndirected = edge.Handle;
        var e0 = new FixedDirectedEdgeHandle(fixedUndirected.Index * 2);
        var e1 = e0.Rev();
        var d0 = triangulation.DirectedEdge(e0);
        var d1 = triangulation.DirectedEdge(e1);
        return d0.IsOuterEdge() || d1.IsOuterEdge();
    }

    private static bool IsEncroachingEdge(
        Point2<double> edgeFrom,
        Point2<double> edgeTo,
        Point2<double> queryPoint)
    {
        var center = new Point2<double>(
            (edgeFrom.X + edgeTo.X) * 0.5,
            (edgeFrom.Y + edgeTo.Y) * 0.5);

        var dx = edgeFrom.X - edgeTo.X;
        var dy = edgeFrom.Y - edgeTo.Y;
        var radius2 = (dx * dx + dy * dy) * 0.25;

        var qdx = queryPoint.X - center.X;
        var qdy = queryPoint.Y - center.Y;
        var dist2 = qdx * qdx + qdy * qdy;

        return dist2 < radius2;
    }

    private static bool TryGetEncroachedConstraintEdge<DE, UE, F, L>(
        ConstrainedDelaunayTriangulation<Point2<double>, DE, UE, F, L> triangulation,
        Point2<double> circumcenter,
        out FixedUndirectedEdgeHandle encroachedEdge)
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        foreach (var edge in triangulation.UndirectedEdges())
        {
            if (!edge.Data.IsConstraintEdge)
            {
                continue;
            }

            var fixedUndirected = edge.Handle;
            var e0 = triangulation.DirectedEdge(new FixedDirectedEdgeHandle(fixedUndirected.Index * 2));
            var from = e0.From().Data.Position;
            var to = e0.To().Data.Position;

            if (IsEncroachingEdge(from, to, circumcenter))
            {
                encroachedEdge = fixedUndirected;
                return true;
            }
        }

        encroachedEdge = default;
        return false;
    }

    private static (double minX, double maxX, double minY, double maxY) ComputeBoundingBox<DE, UE, F, L>(
        ConstrainedDelaunayTriangulation<Point2<double>, DE, UE, F, L> triangulation)
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;

        if (triangulation.NumVertices == 0) return (0, 0, 0, 0);

        foreach (var v in triangulation.Vertices())
        {
            var p = v.Data.Position;
            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;
        }

        return (minX, maxX, minY, maxY);
    }
}
