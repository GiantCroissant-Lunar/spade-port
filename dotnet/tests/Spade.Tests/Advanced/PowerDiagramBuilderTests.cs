using System;
using System.Collections.Generic;
using FluentAssertions;
using Spade.Advanced.Power;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests.Advanced;

public class PowerDiagramBuilderTests
{
    [Fact]
    public void Build_CreatesDiagramWithMatchingSites()
    {
        var points = new List<Point2<double>>
        {
            new(0.0, 0.0),
            new(1.0, 0.0),
            new(0.0, 1.0)
        };

        var weights = new List<double> { 0.0, 1.0, 2.0 };

        var diagram = PowerDiagramBuilder.Build(points, weights);

        diagram.Sites.Count.Should().Be(points.Count);
        diagram.Cells.Count.Should().Be(points.Count);

        for (var i = 0; i < points.Count; i++)
        {
            diagram.Sites[i].Position.Should().Be(points[i]);
            diagram.Sites[i].Weight.Should().Be(weights[i]);

            var cell = diagram.Cells[i];
            cell.SiteIndex.Should().Be(i);
            cell.Site.Position.Should().Be(points[i]);
            cell.Site.Weight.Should().Be(weights[i]);
        }

        // For this simple triangle, all three sites should be mutually adjacent.
        diagram.Cells[0].NeighborSiteIndices.Should().BeEquivalentTo(new[] { 1, 2 });
        diagram.Cells[1].NeighborSiteIndices.Should().BeEquivalentTo(new[] { 0, 2 });
        diagram.Cells[2].NeighborSiteIndices.Should().BeEquivalentTo(new[] { 0, 1 });
    }

    [Fact]
    public void Build_ThrowsOnNullPoints()
    {
        IReadOnlyList<Point2<double>>? points = null;
        IReadOnlyList<double> weights = new List<double>();

        Action act = () => PowerDiagramBuilder.Build(points!, weights);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Build_ThrowsOnNullWeights()
    {
        IReadOnlyList<Point2<double>> points = new List<Point2<double>>();
        IReadOnlyList<double>? weights = null;

        Action act = () => PowerDiagramBuilder.Build(points, weights!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Build_ThrowsOnMismatchedCounts()
    {
        IReadOnlyList<Point2<double>> points = new List<Point2<double>> { new(0.0, 0.0) };
        IReadOnlyList<double> weights = new List<double> { 0.0, 1.0 };

        Action act = () => PowerDiagramBuilder.Build(points, weights);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Build_ThrowsOnEmptyPoints()
    {
        IReadOnlyList<Point2<double>> points = new List<Point2<double>>();
        IReadOnlyList<double> weights = new List<double>();

        Action act = () => PowerDiagramBuilder.Build(points, weights);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Build_IntegratesWithPowerDiagramQueries()
    {
        var points = new List<Point2<double>>
        {
            new(0.0, 0.0),
            new(10.0, 0.0)
        };
        var weights = new List<double> { 0.0, 0.0 };

        var diagram = PowerDiagramBuilder.Build(points, weights);

        var query = new Point2<double>(1.0, 0.0);

        var index = PowerDiagramQueries.FindNearestSiteIndex(diagram.Sites, query);

        index.Should().Be(0);
    }
}
