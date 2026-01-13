using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using Spade.Primitives;

namespace Spade;

/// <summary>
/// Convenience bulk-insertion helpers for triangulations.
/// </summary>
public static class BulkInsertionExtensions
{
    /// <summary>
    /// Inserts a span of Point2&lt;double&gt; coordinates into the triangulation as vertices.
    /// This is a high-performance, zero-allocation API for bulk insertion.
    /// </summary>
    /// <param name="triangulation">The triangulation to insert into</param>
    /// <param name="points">A span of points to insert</param>
    /// <param name="useSpatialSort">Whether to sort points spatially (X then Y) before insertion for better performance</param>
    public static void InsertBulk<DE, UE, F, L>(
        this TriangulationBase<Point2<double>, DE, UE, F, L> triangulation,
        ReadOnlySpan<Point2<double>> points,
        bool useSpatialSort = true)
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        if (triangulation == null) throw new ArgumentNullException(nameof(triangulation));

        // Handle empty span edge case
        if (points.IsEmpty) return;

        // Preallocate capacity to minimize resizing
        triangulation.PreallocateForBulkInsert(points.Length);
        triangulation.OptimizeHintGeneratorForBulk();

        if (useSpatialSort && points.Length > 1)
        {
            // Use ArrayPool to avoid allocations for temporary sorting array
            var pool = ArrayPool<Point2<double>>.Shared;
            var sortableArray = pool.Rent(points.Length);

            try
            {
                // Copy span to rented array
                points.CopyTo(sortableArray.AsSpan(0, points.Length));

                // Create a span over the used portion of the rented array
                var sortableSpan = sortableArray.AsSpan(0, points.Length);

                // Use in-place stable sort by X then Y coordinates
                sortableSpan.Sort((a, b) =>
                {
                    var xComparison = a.X.CompareTo(b.X);
                    return xComparison != 0 ? xComparison : a.Y.CompareTo(b.Y);
                });

                // Insert sorted points
                foreach (var point in sortableSpan)
                {
                    triangulation.Insert(point);
                }
            }
            finally
            {
                // Always return the rented array to the pool
                pool.Return(sortableArray);
            }
        }
        else
        {
            // Insert points in original order
            foreach (var point in points)
            {
                triangulation.Insert(point);
            }
        }
    }

    /// <summary>
    /// Inserts a span of vertices into the triangulation.
    /// This is a high-performance, zero-allocation API for bulk insertion of custom vertex types.
    /// </summary>
    /// <param name="triangulation">The triangulation to insert into</param>
    /// <param name="vertices">A span of vertices to insert</param>
    /// <param name="useSpatialSort">Whether to sort vertices spatially (X then Y) before insertion for better performance</param>
    public static void InsertBulk<V, DE, UE, F, L>(
        this TriangulationBase<V, DE, UE, F, L> triangulation,
        ReadOnlySpan<V> vertices,
        bool useSpatialSort = true)
        where V : IHasPosition<double>, new()
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        if (triangulation == null) throw new ArgumentNullException(nameof(triangulation));

        // Handle empty span edge case
        if (vertices.IsEmpty) return;

        // Preallocate capacity to minimize resizing
        triangulation.PreallocateForBulkInsert(vertices.Length);
        triangulation.OptimizeHintGeneratorForBulk();

        if (useSpatialSort && vertices.Length > 1)
        {
            // Use ArrayPool to avoid allocations for temporary sorting array
            var pool = ArrayPool<V>.Shared;
            var sortableArray = pool.Rent(vertices.Length);

            try
            {
                // Copy span to rented array
                vertices.CopyTo(sortableArray.AsSpan(0, vertices.Length));

                // Create a span over the used portion of the rented array
                var sortableSpan = sortableArray.AsSpan(0, vertices.Length);

                // Use in-place stable sort by X then Y coordinates
                sortableSpan.Sort((a, b) =>
                {
                    var posA = a.Position;
                    var posB = b.Position;
                    var xComparison = posA.X.CompareTo(posB.X);
                    return xComparison != 0 ? xComparison : posA.Y.CompareTo(posB.Y);
                });

                // Insert sorted vertices
                foreach (var vertex in sortableSpan)
                {
                    triangulation.Insert(vertex);
                }
            }
            finally
            {
                // Always return the rented array to the pool
                pool.Return(sortableArray);
            }
        }
        else
        {
            // Insert vertices in original order
            foreach (var vertex in vertices)
            {
                triangulation.Insert(vertex);
            }
        }
    }

    /// <summary>
    /// Inserts a collection of vertices into the triangulation. Optionally performs a simple
    /// spatial sort (by X then Y) before insertion to improve locality and performance on
    /// larger point sets. Enhanced with preallocation for better performance.
    /// </summary>
    public static void InsertBulk<V, DE, UE, F, L>(
        this TriangulationBase<V, DE, UE, F, L> triangulation,
        IEnumerable<V> vertices,
        bool useSpatialSort = true)
        where V : IHasPosition<double>, new()
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        if (triangulation == null) throw new ArgumentNullException(nameof(triangulation));
        if (vertices == null) throw new ArgumentNullException(nameof(vertices));

        var list = vertices as IList<V> ?? vertices.ToList();

        // Handle empty collection edge case
        if (list.Count == 0) return;

        // Add capacity estimation and preallocation
        triangulation.PreallocateForBulkInsert(list.Count);
        triangulation.OptimizeHintGeneratorForBulk();

        if (useSpatialSort && list.Count > 1)
        {
            // Use ArrayPool for allocation-efficient sorting instead of LINQ
            var pool = ArrayPool<V>.Shared;
            var sortableArray = pool.Rent(list.Count);

            try
            {
                // Copy list to rented array
                if (list is IList<V> ilist)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        sortableArray[i] = ilist[i];
                    }
                }
                else
                {
                    int index = 0;
                    foreach (var item in list)
                    {
                        sortableArray[index++] = item;
                    }
                }

                // Create a span over the used portion of the rented array
                var sortableSpan = sortableArray.AsSpan(0, list.Count);

                // Use in-place stable sort by X then Y coordinates
                sortableSpan.Sort((a, b) =>
                {
                    var posA = ((IHasPosition<double>)a).Position;
                    var posB = ((IHasPosition<double>)b).Position;
                    var xComparison = posA.X.CompareTo(posB.X);
                    return xComparison != 0 ? xComparison : posA.Y.CompareTo(posB.Y);
                });

                // Insert sorted vertices
                foreach (var vertex in sortableSpan)
                {
                    triangulation.Insert(vertex);
                }
            }
            finally
            {
                // Always return the rented array to the pool
                pool.Return(sortableArray);
            }
        }
        else
        {
            // Insert vertices in original order
            foreach (var v in list)
            {
                triangulation.Insert(v);
            }
        }
    }
}
