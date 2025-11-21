using System.Collections.Generic;
using FluentAssertions;
using Spade.Advanced.Power;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests.Advanced;

public class WeightedDelaunayTriangulationTests
{
    [Fact]
    public void InsertRange_StoresSitesWithCorrectWeights()
    {
        var wdt = new WeightedDelaunayTriangulation();

        var sites = new List<WeightedPoint>
        {
            new(new Point2<double>(0.0, 0.0), 1.0),
            new(new Point2<double>(1.0, 0.0), 2.0)
        };

        wdt.InsertRange(sites);

        wdt.VertexCount.Should().Be(2);
        wdt.Sites.Count.Should().Be(2);

        for (var i = 0; i < sites.Count; i++)
        {
            var stored = wdt.Sites[i];
            stored.Position.Should().Be(sites[i].Position);
            stored.Weight.Should().Be(sites[i].Weight);
        }
    }

    [Fact]
    public void BuildNeighborGraph_TriangleIsFullyConnected()
    {
        var wdt = new WeightedDelaunayTriangulation();

        wdt.Insert(new WeightedPoint(new Point2<double>(0.0, 0.0), 0.0));
        wdt.Insert(new WeightedPoint(new Point2<double>(1.0, 0.0), 0.0));
        wdt.Insert(new WeightedPoint(new Point2<double>(0.0, 1.0), 0.0));

        var neighbors = wdt.BuildNeighborGraph();

        neighbors.Count.Should().Be(3);

        neighbors[0].Should().BeEquivalentTo(new[] { 1, 2 });
        neighbors[1].Should().BeEquivalentTo(new[] { 0, 2 });
        neighbors[2].Should().BeEquivalentTo(new[] { 0, 1 });
    }
}
