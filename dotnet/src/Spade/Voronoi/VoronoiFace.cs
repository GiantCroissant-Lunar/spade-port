using System.Collections.Generic;
using Spade.Handles;
using Spade.Primitives;

namespace Spade.Voronoi;

public readonly struct VoronoiFace<V, DE, UE, F>
    where V : IHasPosition<double>, new()
    where DE : new()
    where UE : new()
    where F : new()
{
    private readonly VertexHandle<V, DE, UE, F> _delaunayVertex;

    public VoronoiFace(VertexHandle<V, DE, UE, F> delaunayVertex)
    {
        _delaunayVertex = delaunayVertex;
    }

    public VertexHandle<V, DE, UE, F> AsDelaunayVertex() => _delaunayVertex;

    public IEnumerable<DirectedVoronoiEdge<V, DE, UE, F>> AdjacentEdges()
    {
        var startEdge = _delaunayVertex.OutEdge();
        if (startEdge == null) yield break;

        var current = startEdge.Value;
        var visitedEdges = new HashSet<int>();
        int maxIterations = 1000; // Safety limit
        int iterations = 0;

        do
        {
            // Safety checks to prevent infinite loops
            if (iterations++ >= maxIterations)
            {
                throw new InvalidOperationException(
                    $"Exceeded maximum iterations ({maxIterations}) while traversing Voronoi face. " +
                    "This likely indicates a malformed DCEL structure.");
            }

            // Track visited edges to detect cycles
            if (!visitedEdges.Add(current.Handle.Index))
            {
                throw new InvalidOperationException(
                    $"Detected cycle in Voronoi face traversal at edge {current.Handle.Index}. " +
                    "This indicates a malformed DCEL structure.");
            }

            yield return new DirectedVoronoiEdge<V, DE, UE, F>(current);
            current = current.CCW();

            // Use handle index comparison for more reliable equality check
        } while (current.Handle.Index != startEdge.Value.Handle.Index);
    }
    
    public override string ToString() => $"VoronoiFace(Dual: {_delaunayVertex})";
}
