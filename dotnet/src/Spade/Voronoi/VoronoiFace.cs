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
        do
        {
            yield return new DirectedVoronoiEdge<V, DE, UE, F>(current);
            current = current.CCW();
        } while (current.Handle != startEdge.Value.Handle);
    }
    
    public override string ToString() => $"VoronoiFace(Dual: {_delaunayVertex})";
}
