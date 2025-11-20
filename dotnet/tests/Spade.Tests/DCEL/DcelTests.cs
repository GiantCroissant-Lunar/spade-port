using FluentAssertions;
using Spade.DCEL;
using Spade.Handles;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests.DCEL;

public class DcelTests
{
    [Fact]
    public void TestDcelInitialization()
    {
        var dcel = new Dcel<Point2<double>, int, int, int>();
        
        dcel.NumVertices.Should().Be(0);
        dcel.NumFaces.Should().Be(1); // Outer face
        dcel.NumDirectedEdges.Should().Be(0);
        dcel.NumUndirectedEdges.Should().Be(0);

        var outerFace = dcel.OuterFace();
        outerFace.IsOuter.Should().BeTrue();
        outerFace.Handle.Index.Should().Be(0);
    }

    [Fact]
    public void TestHandleEquality()
    {
        var v1 = new FixedVertexHandle(1);
        var v2 = new FixedVertexHandle(1);
        var v3 = new FixedVertexHandle(2);

        v1.Should().Be(v2);
        v1.Should().NotBe(v3);
    }

    [Fact]
    public void TestEdgeHandleNavigation()
    {
        var e1 = new FixedDirectedEdgeHandle(0);
        var e2 = new FixedDirectedEdgeHandle(1);

        e1.NormalizeIndex().Should().Be(0);
        e2.NormalizeIndex().Should().Be(1);

        e1.Rev().Should().Be(e2);
        e2.Rev().Should().Be(e1);

        e1.AsUndirected().Should().Be(new FixedUndirectedEdgeHandle(0));
        e2.AsUndirected().Should().Be(new FixedUndirectedEdgeHandle(0));
    }
}
