using Spade.Handles;
using Spade.Primitives;

namespace Spade.Voronoi;

public abstract class VoronoiVertex<V, DE, UE, F>
    where V : IHasPosition<double>, new()
    where DE : new()
    where UE : new()
    where F : new()
{
    public abstract Point2<double>? Position { get; }
    
    public sealed class Inner : VoronoiVertex<V, DE, UE, F>
    {
        public FaceHandle<V, DE, UE, F> Face { get; }

        public Inner(FaceHandle<V, DE, UE, F> face)
        {
            Face = face;
        }

        public override Point2<double>? Position => Face.Circumcenter();
        
        public override string ToString() => $"InnerVoronoiVertex({Position})";
    }

    public sealed class Outer : VoronoiVertex<V, DE, UE, F>
    {
        public DirectedVoronoiEdge<V, DE, UE, F> Edge { get; }

        public Outer(DirectedVoronoiEdge<V, DE, UE, F> edge)
        {
            Edge = edge;
        }

        public override Point2<double>? Position => null;
        
        public override string ToString() => $"OuterVoronoiVertex(Dual: {Edge.AsDelaunayEdge()})";
    }
}
