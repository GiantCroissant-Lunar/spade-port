using FluentAssertions;
using Spade.Primitives;

namespace Spade.Tests.Primitives;

public class Point3Tests
{
    [Fact]
    public void TestConstruction()
    {
        var p = new Point3<double>(1.0, 2.0, 3.0);
        p.X.Should().Be(1.0);
        p.Y.Should().Be(2.0);
        p.Z.Should().Be(3.0);
    }

    [Fact]
    public void TestDistance2()
    {
        var p1 = new Point3<double>(0.0, 0.0, 0.0);
        var p2 = new Point3<double>(1.0, 2.0, 2.0);
        p1.Distance2(p2).Should().Be(9.0);
    }

    [Fact]
    public void TestEquality()
    {
        var p1 = new Point3<int>(1, 2, 3);
        var p2 = new Point3<int>(1, 2, 3);
        var p3 = new Point3<int>(3, 4, 5);

        p1.Should().Be(p2);
        p1.Should().NotBe(p3);
        (p1 == p2).Should().BeTrue();
        (p1 != p3).Should().BeTrue();
    }
}
