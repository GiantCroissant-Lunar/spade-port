using System;
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
    /// Inserts a collection of vertices into the triangulation. Optionally performs a simple
    /// spatial sort (by X then Y) before insertion to improve locality and performance on
    /// larger point sets.
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
        if (useSpatialSort && list.Count > 1)
        {
            list = list.OrderBy(v => ((IHasPosition<double>)v).Position.X)
                       .ThenBy(v => ((IHasPosition<double>)v).Position.Y)
                       .ToList();
        }

        foreach (var v in list)
        {
            triangulation.Insert(v);
        }
    }
}
