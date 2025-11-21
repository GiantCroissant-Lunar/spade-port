using System.Linq;
using FluentAssertions;
using Spade;
using Spade.Primitives;

namespace Spade.Tests.Validation;

internal static class TriangulationInvariants
{
    public static void AssertBasicTopology<V, DE, UE, F, L>(TriangulationBase<V, DE, UE, F, L> triangulation)
        where V : IHasPosition<double>, new()
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        triangulation.NumFaces.Should().BeGreaterThan(0);

        // Outer face should be flagged as outer, inner faces should not.
        var outer = triangulation.OuterFace();
        outer.IsOuter.Should().BeTrue();
        foreach (var face in triangulation.InnerFaces())
        {
            face.IsOuter.Should().BeFalse();
        }

        // Counts from enumerators should match the exposed counters.
        triangulation.Vertices().Count().Should().Be(triangulation.NumVertices);
        triangulation.DirectedEdges().Count().Should().Be(triangulation.NumDirectedEdges);
        triangulation.UndirectedEdges().Count().Should().Be(triangulation.NumUndirectedEdges);

        // Each undirected edge should correspond to exactly two directed edges.
        triangulation.NumDirectedEdges.Should().Be(triangulation.NumUndirectedEdges * 2);
    }
}
