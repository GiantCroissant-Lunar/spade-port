using System;
using System.Collections.Generic;
using System.Linq;
using Spade.Handles;
using Spade.Primitives;
using Spade.DCEL;

namespace Spade;

public static class DelaunayTriangulationExtensions
{
    public static BarycentricInterpolator<V, DE, UE, F, L> Barycentric<V, DE, UE, F, L>(
        this DelaunayTriangulation<V, DE, UE, F, L> triangulation)
        where V : IHasPosition<double>, new()
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        return new BarycentricInterpolator<V, DE, UE, F, L>(triangulation);
    }

    /// <summary>
    /// Creates a natural neighbor interpolator over the given triangulation.
    /// </summary>
    /// <remarks>
    /// The returned interpolator instance is not thread-safe. For concurrent use from multiple threads
    /// over a shared triangulation, use <see cref="ThreadSafeNaturalNeighbor{V,DE,UE,F,L}(DelaunayTriangulation{V,DE,UE,F,L})"/>.
    /// </remarks>
    public static NaturalNeighborInterpolator<V, DE, UE, F, L> NaturalNeighbor<V, DE, UE, F, L>(
        this DelaunayTriangulation<V, DE, UE, F, L> triangulation)
        where V : IHasPosition<double>, new()
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        return new NaturalNeighborInterpolator<V, DE, UE, F, L>(triangulation);
    }

    /// <summary>
    /// Creates a thread-safe natural neighbor interpolator over the given triangulation.
    /// </summary>
    /// <remarks>
    /// This method is appropriate when a single triangulation instance is shared across multiple threads
    /// that all need to perform interpolation concurrently.
    /// </remarks>
    public static ThreadSafeNaturalNeighborInterpolator<V, DE, UE, F, L> ThreadSafeNaturalNeighbor<V, DE, UE, F, L>(
        this DelaunayTriangulation<V, DE, UE, F, L> triangulation)
        where V : IHasPosition<double>, new()
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        return new ThreadSafeNaturalNeighborInterpolator<V, DE, UE, F, L>(triangulation);
    }

    public static bool TryIsInsideConvexHull<V, DE, UE, F, L>(
        this DelaunayTriangulation<V, DE, UE, F, L> triangulation,
        Point2<double> position,
        out bool isInside)
        where V : IHasPosition<double>, new()
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        isInside = false;
        if (triangulation.NumVertices < 3)
        {
            // Not a valid hull yet
            return false;
        }

        var loc = triangulation.LocateWithHintOptionCore(position, null);
        if (loc is PositionInTriangulation.OutsideOfConvexHull)
        {
            isInside = false;
            return true;
        }

        isInside = true;
        return true;
    }

    public static bool TryGetHullCenterPattern<V, DE, UE, F, L>(
        this DelaunayTriangulation<V, DE, UE, F, L> triangulation,
        out FixedVertexHandle center,
        out List<FixedVertexHandle> hullRing)
        where V : IHasPosition<double>, new()
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        center = default;
        hullRing = new List<FixedVertexHandle>();

        if (triangulation.NumVertices < 3)
        {
            return false;
        }

        var outerFace = triangulation.OuterFace();
        var adjacent = outerFace.AdjacentEdge();

        if (adjacent == null)
        {
            return false;
        }

        var start = adjacent.Value;
        var current = start;
        var visited = new HashSet<int>();

        do
        {
            if (!visited.Add(current.Handle.Index))
            {
                // Cycle detected in outer face traversal (should be a loop anyway)
                break;
            }
            hullRing.Add(current.From().Handle);
            current = current.Next();
        } while (current.Handle != start.Handle);

        if (triangulation.NumVertices != hullRing.Count + 1)
        {
            return false;
        }

        var hullIndices = new HashSet<int>();
        foreach (var v in hullRing)
        {
            hullIndices.Add(v.Index);
        }

        for (int i = 0; i < triangulation.NumVertices; i++)
        {
            if (!hullIndices.Contains(i))
            {
                center = new FixedVertexHandle(i);
                return true;
            }
        }

        return false;
    }
}
