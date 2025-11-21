using System;
using System.Collections.Generic;
using FluentAssertions;
using Spade.Advanced.Power;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests.Advanced;

public class PowerDiagramQueriesTests
{
    [Fact]
    public void FindNearestSiteIndex_PrefersEuclideanNearest_WhenWeightsEqual()
    {
        var sites = new List<WeightedPoint>
        {
            new(new Point2<double>(0.0, 0.0), 0.0),
            new(new Point2<double>(1.0, 0.0), 0.0),
            new(new Point2<double>(2.0, 0.0), 0.0)
        };

        var query = new Point2<double>(0.2, 0.0);

        var index = PowerDiagramQueries.FindNearestSiteIndex(sites, query);

        index.Should().Be(0); // closest in Euclidean sense
    }

    [Fact]
    public void FindNearestSiteIndex_PrefersHeavierButFurtherSite_WhenPowerDistanceSmaller()
    {
        var nearLight = new WeightedPoint(new Point2<double>(0.0, 0.0), 0.0);
        var farHeavy = new WeightedPoint(new Point2<double>(5.0, 0.0), 20.0);
        var sites = new List<WeightedPoint> { nearLight, farHeavy };

        var query = new Point2<double>(2.5, 0.0);

        var index = PowerDiagramQueries.FindNearestSiteIndex(sites, query);

        // Compute the actual power distances to assert the behavior clearly
        var dNear = PowerGeometry.PowerDistance(nearLight, query);
        var dFar = PowerGeometry.PowerDistance(farHeavy, query);

        dFar.Should().BeLessThan(dNear);
        index.Should().Be(1); // farHeavy should be preferred in power-distance sense
    }

    [Fact]
    public void FindNearestSite_ReturnsSameSite_AsFindNearestSiteIndex()
    {
        var sites = new List<WeightedPoint>
        {
            new(new Point2<double>(0.0, 0.0), 0.0),
            new(new Point2<double>(1.0, 0.0), 5.0)
        };

        var query = new Point2<double>(2.0, 0.0);

        var index = PowerDiagramQueries.FindNearestSiteIndex(sites, query);
        var site = PowerDiagramQueries.FindNearestSite(sites, query);

        site.Should().Be(sites[index]);
    }

    [Fact]
    public void FindNearestSiteIndex_Throws_OnEmptySites()
    {
        var sites = new List<WeightedPoint>();
        var query = new Point2<double>(0.0, 0.0);

        Action act = () => PowerDiagramQueries.FindNearestSiteIndex(sites, query);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FindNearestSiteIndex_Throws_OnNullSites()
    {
        IReadOnlyList<WeightedPoint>? sites = null;
        var query = new Point2<double>(0.0, 0.0);

        Action act = () => PowerDiagramQueries.FindNearestSiteIndex(sites!, query);

        act.Should().Throw<ArgumentNullException>();
    }
}
