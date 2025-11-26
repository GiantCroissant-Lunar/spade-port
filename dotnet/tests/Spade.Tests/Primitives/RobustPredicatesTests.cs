using System;
using FluentAssertions;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests.Primitives;

public class RobustPredicatesTests
{
    [Fact]
    public void Orient2D_BasicOrientation()
    {
        var p1 = new Point2<double>(0.0, 0.0);
        var p2 = new Point2<double>(1.0, 0.0);

        var left = new Point2<double>(0.0, 1.0);
        var right = new Point2<double>(0.0, -1.0);
        var on = new Point2<double>(0.5, 0.0);

        RobustPredicates.Orient2D(p1, p2, left).Should().BeGreaterThan(0.0);
        RobustPredicates.Orient2D(p1, p2, right).Should().BeLessThan(0.0);
        RobustPredicates.Orient2D(p1, p2, on).Should().Be(0.0);
    }

    [Fact]
    public void Orient2D_LargeNearlyCollinearCoordinates()
    {
        var p1 = new Point2<double>(0.0, 0.0);
        var p2 = new Point2<double>(1e9, 1e9);

        var on = new Point2<double>(5e8, 5e8);
        var above = new Point2<double>(5e8, 5e8 + 1e3);
        var below = new Point2<double>(5e8, 5e8 - 1e3);

        RobustPredicates.Orient2D(p1, p2, on).Should().Be(0.0);
        RobustPredicates.Orient2D(p1, p2, above).Should().BeGreaterThan(0.0);
        RobustPredicates.Orient2D(p1, p2, below).Should().BeLessThan(0.0);
    }
}
