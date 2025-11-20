using Spade.Handles;
using Spade.Primitives;

namespace Spade;

public class DelaunayTriangulation<V, DE, UE, F, L> : TriangulationBase<V, DE, UE, F, L>
    where V : IHasPosition<double>, new()
    where DE : new()
    where UE : new()
    where F : new()
    where L : IHintGenerator<double>, new()
{
    public DelaunayTriangulation() : base()
    {
    }
}
