using Spade.Handles;
using Spade.Primitives;

namespace Spade.Voronoi;

public readonly struct DirectedVoronoiEdge<V, DE, UE, F>
    where V : IHasPosition<double>, new()
    where DE : new()
    where UE : new()
    where F : new()
{
    private readonly DirectedEdgeHandle<V, DE, UE, F> _delaunayEdge;

    public DirectedVoronoiEdge(DirectedEdgeHandle<V, DE, UE, F> delaunayEdge)
    {
        _delaunayEdge = delaunayEdge;
    }

    public DirectedEdgeHandle<V, DE, UE, F> AsDelaunayEdge() => _delaunayEdge;

    public VoronoiVertex<V, DE, UE, F> From()
    {
        var face = _delaunayEdge.Face();
        if (!face.IsOuter)
        {
            return new VoronoiVertex<V, DE, UE, F>.Inner(face);
        }
        else
        {
            return new VoronoiVertex<V, DE, UE, F>.Outer(this);
        }
    }

    public VoronoiVertex<V, DE, UE, F> To()
    {
        return Rev().From();
    }

    public DirectedVoronoiEdge<V, DE, UE, F> Rev()
    {
        return new DirectedVoronoiEdge<V, DE, UE, F>(_delaunayEdge.Rev());
    }

    public DirectedVoronoiEdge<V, DE, UE, F> Next()
    {
        return new DirectedVoronoiEdge<V, DE, UE, F>(_delaunayEdge.CCW());
    }

    public DirectedVoronoiEdge<V, DE, UE, F> Prev()
    {
        return new DirectedVoronoiEdge<V, DE, UE, F>(_delaunayEdge.CW());
    }

    public Point2<double> DirectionVector()
    {
        var from = _delaunayEdge.From().Data.Position;
        var to = _delaunayEdge.To().Data.Position;
        var diff = to.Sub(from);
        return new Point2<double>(-diff.Y, diff.X);
    }

    public override string ToString() => $"DirectedVoronoiEdge(Dual: {_delaunayEdge})";
}

public readonly struct UndirectedVoronoiEdge<V, DE, UE, F>
    where V : IHasPosition<double>, new()
    where DE : new()
    where UE : new()
    where F : new()
{
    private readonly UndirectedEdgeHandle<V, DE, UE, F> _delaunayEdge;

    public UndirectedVoronoiEdge(UndirectedEdgeHandle<V, DE, UE, F> delaunayEdge)
    {
        _delaunayEdge = delaunayEdge;
    }

    public UndirectedEdgeHandle<V, DE, UE, F> AsDelaunayEdge() => _delaunayEdge;

    public override string ToString() => $"UndirectedVoronoiEdge(Dual: {_delaunayEdge})";
}
