using FluentAssertions;
using Spade.Advanced.Power;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests.Advanced;

public class PowerGeometryTests
{
    [Fact]
    public void PowerDistance_Decreases_WhenWeightIncreases()
    {
        var p = new Point2<double>(0, 0);
        var siteLow = new WeightedPoint(p, 0.0);
        var siteHigh = new WeightedPoint(p, 1.0);

        var q = new Point2<double>(1, 0);

        var dLow = PowerGeometry.PowerDistance(siteLow, q);
        var dHigh = PowerGeometry.PowerDistance(siteHigh, q);

        dHigh.Should().BeLessThan(dLow);
    }

    [Fact]
    public void ComparePowerDistance_PrefersCloserOrMoreWeightedSite()
    {
        var siteA = new WeightedPoint(new Point2<double>(0, 0), 0.0);
        var siteB = new WeightedPoint(new Point2<double>(2, 0), 0.0);
        var x = new Point2<double>(0.5, 0.0);

        PowerGeometry.ComparePowerDistance(siteA, siteB, x).Should().Be(-1); // a closer than b

        var siteC = new WeightedPoint(new Point2<double>(0, 0), 0.0);
        var siteD = new WeightedPoint(new Point2<double>(0, 0), 1.0);
        var y = new Point2<double>(2.0, 0.0);

        // Same position, but D has larger weight -> smaller power distance
        PowerGeometry.ComparePowerDistance(siteC, siteD, y).Should().Be(1); // b closer than a
    }
}
