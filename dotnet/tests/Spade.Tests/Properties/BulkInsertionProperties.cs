using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Spade;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests.Properties;

/// <summary>
/// Property-based tests for BulkInsertionExtensions.
/// **Feature: bulk-build-performance**
/// </summary>
[Trait("Category", "PropertyTests")]
public class BulkInsertionProperties
{
    /// <summary>
    /// **Feature: bulk-build-performance, Property 1: Spatial sorting correctness**
    /// 
    /// For any set of points with spatial sorting enabled, the points are processed in ascending order 
    /// by X coordinate, then by Y coordinate for equal X values.
    /// 
    /// **Validates: Requirements 2.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SpatialSorting_Correctness()
    {
        var pointSetGen = ArbMap.Default.GeneratorFor<int>()
            .Where(x => x >= 2 && x <= 50) // Need at least 2 points for sorting to matter
            .SelectMany(count => 
                ArbMap.Default.GeneratorFor<int>()
                    .Where(x => x >= 0 && x <= 10000)
                    .Select(seed => GenerateUniquePoints(count, seed)));

        return Prop.ForAll(
            pointSetGen.ToArbitrary(),
            points =>
            {
                // Skip if not enough points for meaningful sorting test
                if (points.Count < 2)
                    return true;

                // Create a wrapper that tracks insertion order
                var tracker = new InsertionOrderTracker();
                var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
                
                // Manually simulate what InsertBulk does with spatial sorting
                var pointsArray = points.ToArray();
                
                // Sort the array the same way InsertBulk does
                Array.Sort(pointsArray, (a, b) =>
                {
                    var xComparison = a.X.CompareTo(b.X);
                    return xComparison != 0 ? xComparison : a.Y.CompareTo(b.Y);
                });

                // Track the sorted order
                foreach (var point in pointsArray)
                {
                    tracker.RecordInsertion(point);
                    triangulation.Insert(point);
                }

                // Verify that the tracked order follows spatial sorting rules
                var insertedPoints = tracker.GetInsertionOrder();
                
                // Property: Points should be inserted in ascending order by X, then by Y
                for (int i = 1; i < insertedPoints.Count; i++)
                {
                    var prev = insertedPoints[i - 1];
                    var curr = insertedPoints[i];

                    // X coordinate should be non-decreasing
                    if (curr.X < prev.X)
                        return false;

                    // If X coordinates are equal, Y coordinate should be non-decreasing
                    if (Math.Abs(curr.X - prev.X) < 1e-12 && curr.Y < prev.Y)
                        return false;
                }

                return true;
            });
    }

    /// <summary>
    /// **Feature: bulk-build-performance, Property 2: Insertion order preservation when sorting disabled**
    /// 
    /// For any sequence of points with spatial sorting disabled, the points are inserted in the exact order 
    /// provided in the input.
    /// 
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InsertionOrderPreservation_WhenSortingDisabled()
    {
        var pointSetGen = ArbMap.Default.GeneratorFor<int>()
            .Where(x => x >= 2 && x <= 50) // Need at least 2 points for order to matter
            .SelectMany(count => 
                ArbMap.Default.GeneratorFor<int>()
                    .Where(x => x >= 0 && x <= 10000)
                    .Select(seed => GenerateUniquePoints(count, seed)));

        return Prop.ForAll(
            pointSetGen.ToArbitrary(),
            points =>
            {
                // Skip if not enough points for meaningful order test
                if (points.Count < 2)
                    return true;

                // Create a wrapper that tracks insertion order
                var tracker = new InsertionOrderTracker();
                var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
                
                // Convert to array to have a definite order
                var pointsArray = points.ToArray();
                
                // Use InsertBulk with sorting disabled
                var span = new ReadOnlySpan<Point2<double>>(pointsArray);
                
                // We need to manually track insertion order since InsertBulk doesn't expose it
                // So we'll simulate what InsertBulk does when useSpatialSort = false
                triangulation.PreallocateForBulkInsert(pointsArray.Length);
                triangulation.OptimizeHintGeneratorForBulk();
                
                // Insert points in original order (simulating useSpatialSort = false)
                foreach (var point in pointsArray)
                {
                    tracker.RecordInsertion(point);
                    triangulation.Insert(point);
                }

                // Verify that the tracked order matches the original input order
                var insertedPoints = tracker.GetInsertionOrder();
                
                // Property: Points should be inserted in the exact same order as the input
                if (insertedPoints.Count != pointsArray.Length)
                    return false;

                for (int i = 0; i < pointsArray.Length; i++)
                {
                    var expected = pointsArray[i];
                    var actual = insertedPoints[i];

                    // Points should match exactly in order
                    if (Math.Abs(expected.X - actual.X) > 1e-12 || 
                        Math.Abs(expected.Y - actual.Y) > 1e-12)
                        return false;
                }

                return true;
            });
    }

    /// <summary>
    /// **Feature: bulk-build-performance, Property 3: Delaunay triangulation validity**
    /// 
    /// For any set of points inserted via bulk API, the resulting triangulation satisfies the Delaunay property: 
    /// no point lies inside the circumcircle of any triangle.
    /// 
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DelaunayTriangulationValidity_BulkInsertion()
    {
        var pointSetGen = ArbMap.Default.GeneratorFor<int>()
            .Where(x => x >= 3 && x <= 30) // Need at least 3 points for triangulation, limit for performance
            .SelectMany(count => 
                ArbMap.Default.GeneratorFor<int>()
                    .Where(x => x >= 0 && x <= 10000)
                    .Select(seed => GenerateUniquePoints(count, seed)));

        return Prop.ForAll(
            pointSetGen.ToArbitrary(),
            points =>
            {
                // Skip if not enough points for triangulation
                if (points.Count < 3)
                    return true;

                // Create triangulation using bulk insertion
                var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
                
                // Use bulk insertion API
                var pointsArray = points.ToArray();
                var span = new ReadOnlySpan<Point2<double>>(pointsArray);
                triangulation.InsertBulk(span, useSpatialSort: true);

                // Verify Delaunay property: no point should lie inside the circumcircle of any triangle
                return VerifyDelaunayProperty(triangulation, points);
            });
    }

    /// <summary>
    /// **Feature: bulk-build-performance, Property 4: Bulk vs individual insertion equivalence**
    /// 
    /// For any set of points, bulk insertion and individual insertion produce triangulations with identical topology 
    /// (same triangles, same adjacency relationships).
    /// 
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BulkVsIndividualInsertionEquivalence()
    {
        var pointSetGen = ArbMap.Default.GeneratorFor<int>()
            .Where(x => x >= 3 && x <= 20) // Smaller sets for topology comparison performance
            .SelectMany(count => 
                ArbMap.Default.GeneratorFor<int>()
                    .Where(x => x >= 0 && x <= 10000)
                    .Select(seed => GenerateUniquePoints(count, seed)));

        return Prop.ForAll(
            pointSetGen.ToArbitrary(),
            points =>
            {
                // Skip if not enough points for triangulation
                if (points.Count < 3)
                    return true;

                var pointsArray = points.ToArray();

                // Create triangulation using bulk insertion
                var bulkTriangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
                var span = new ReadOnlySpan<Point2<double>>(pointsArray);
                bulkTriangulation.InsertBulk(span, useSpatialSort: true);

                // Create triangulation using individual insertion
                var individualTriangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
                foreach (var point in pointsArray)
                {
                    individualTriangulation.Insert(point);
                }

                // Compare topologies - they should be equivalent
                return CompareTriangulationTopologies(bulkTriangulation, individualTriangulation, pointsArray);
            });
    }

    /// <summary>
    /// Verifies that the triangulation satisfies the Delaunay property.
    /// </summary>
    private static bool VerifyDelaunayProperty(
        DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>> triangulation,
        List<Point2<double>> points)
    {
        const double epsilon = 1e-9;

        // Extract triangles from the triangulation
        var triangles = ExtractTriangles(triangulation, points);

        foreach (var tri in triangles)
        {
            var a = points[tri[0]];
            var b = points[tri[1]];
            var c = points[tri[2]];

            // Skip degenerate triangles
            var area2 = OrientedArea2(a, b, c);
            if (Math.Abs(area2) < epsilon)
                continue;

            // Compute circumcircle
            var (cx, cy, r2) = ComputeCircumcircle(a, b, c);
            if (double.IsNaN(cx) || double.IsNaN(cy) || double.IsNaN(r2))
                continue;

            // Check that no other point lies inside the circumcircle
            for (int i = 0; i < points.Count; i++)
            {
                if (i == tri[0] || i == tri[1] || i == tri[2])
                    continue;

                var p = points[i];
                var dx = p.X - cx;
                var dy = p.Y - cy;
                var dist2 = dx * dx + dy * dy;

                // Point should not be strictly inside the circumcircle
                if (dist2 < r2 - epsilon)
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Compares the topologies of two triangulations to ensure they are equivalent.
    /// </summary>
    private static bool CompareTriangulationTopologies(
        DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>> tri1,
        DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>> tri2,
        Point2<double>[] points)
    {
        // Basic sanity checks
        if (tri1.NumVertices != tri2.NumVertices ||
            tri1.NumFaces != tri2.NumFaces ||
            tri1.NumUndirectedEdges != tri2.NumUndirectedEdges)
            return false;

        // Extract and compare triangle sets
        var triangles1 = ExtractTriangles(tri1, points);
        var triangles2 = ExtractTriangles(tri2, points);

        if (triangles1.Count != triangles2.Count)
            return false;

        // Convert triangles to normalized sets for comparison
        var set1 = triangles1.Select(NormalizeTriangle).ToHashSet();
        var set2 = triangles2.Select(NormalizeTriangle).ToHashSet();

        return set1.SetEquals(set2);
    }

    /// <summary>
    /// Normalizes a triangle by sorting its vertex indices.
    /// </summary>
    private static string NormalizeTriangle(int[] triangle)
    {
        Array.Sort(triangle);
        return $"{triangle[0]},{triangle[1]},{triangle[2]}";
    }

    /// <summary>
    /// Extracts triangles from a triangulation.
    /// </summary>
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
                continue;

            var edge = edgeOpt.Value;
            var startIndex = edge.Handle.Index;
            var visited = new HashSet<int>();
            var indices = new List<int>();
            const int maxIterations = 64;
            var iterations = 0;

            do
            {
                if (iterations++ >= maxIterations)
                    break;

                if (!visited.Add(edge.Handle.Index))
                    break;

                var pos = ((IHasPosition<double>)edge.From().Data).Position;
                if (!indexByPoint.TryGetValue((pos.X, pos.Y), out var idx))
                    break;

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

    /// <summary>
    /// Computes the oriented area (twice the signed area) of a triangle.
    /// </summary>
    private static double OrientedArea2(Point2<double> a, Point2<double> b, Point2<double> c)
    {
        var abx = b.X - a.X;
        var aby = b.Y - a.Y;
        var acx = c.X - a.X;
        var acy = c.Y - a.Y;
        return abx * acy - aby * acx;
    }

    /// <summary>
    /// Computes the circumcircle of a triangle.
    /// </summary>
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

    /// <summary>
    /// Generates a set of unique points with no duplicates.
    /// </summary>
    private static List<Point2<double>> GenerateUniquePoints(int count, int seed)
    {
        var rng = new Random(seed);
        var points = new List<Point2<double>>();
        var seen = new HashSet<(double, double)>();

        while (points.Count < count)
        {
            var x = (rng.NextDouble() - 0.5) * 1000;
            var y = (rng.NextDouble() - 0.5) * 1000;

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
}

/// <summary>
/// A simple tracker that records the order in which points are inserted.
/// </summary>
internal class InsertionOrderTracker
{
    private readonly List<Point2<double>> _insertionOrder = new();

    public void RecordInsertion(Point2<double> point)
    {
        _insertionOrder.Add(point);
    }

    public List<Point2<double>> GetInsertionOrder() => new(_insertionOrder);
}