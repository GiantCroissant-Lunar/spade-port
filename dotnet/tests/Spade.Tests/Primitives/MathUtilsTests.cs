using FluentAssertions;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests.Primitives;

public class MathUtilsTests
{
    [Fact]
    public void TestPointProjection()
    {
        var from = new Point2<double>(1.0, 1.0);
        var to = new Point2<double>(4.0, 5.0);

        var projection = MathUtils.ProjectPoint(from, to, from);
        projection.IsBeforeEdge.Should().BeFalse();
        projection.IsBehindEdge.Should().BeFalse();
        projection.IsOnEdge.Should().BeTrue();
        projection.RelativePosition().Should().Be(0.0);

        var reversed = projection.Reversed();
        reversed.RelativePosition().Should().Be(1.0);
    }

    [Fact]
    public void TestSideQuery()
    {
        var p1 = new Point2<double>(0.0, 0.0);
        var p2 = new Point2<double>(1.0, 1.0);

        MathUtils.SideQuery(p1, p2, new Point2<double>(1.0, 0.0)).IsOnRightSide.Should().BeTrue();
        MathUtils.SideQuery(p1, p2, new Point2<double>(0.0, 1.0)).IsOnLeftSide.Should().BeTrue();
        MathUtils.SideQuery(p1, p2, new Point2<double>(0.5, 0.5)).IsOnLine.Should().BeTrue();
    }
}
