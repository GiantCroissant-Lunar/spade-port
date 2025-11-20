using FluentAssertions;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests.Primitives;

public class Point2Tests
{
    [Fact]
    public void TestConstruction()
    {
        var p = new Point2<double>(1.0, 2.0);
        p.X.Should().Be(1.0);
        p.Y.Should().Be(2.0);
    }

    [Fact]
    public void TestDistance2()
    {
        var p1 = new Point2<double>(0.0, 0.0);
        var p2 = new Point2<double>(3.0, 4.0);
        p1.Distance2(p2).Should().Be(25.0);
    }

    [Fact]
    public void TestEquality()
    {
        var p1 = new Point2<int>(1, 2);
        var p2 = new Point2<int>(1, 2);
        var p3 = new Point2<int>(3, 4);

        p1.Should().Be(p2);
        p1.Should().NotBe(p3);
        (p1 == p2).Should().BeTrue();
        (p1 != p3).Should().BeTrue();
    }
}
