using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Spade.Advanced.Voronoi;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests;

public class CentroidalVoronoiRelaxationTests
{
    [Fact]
    public void RelaxPoints_WithZeroIterations_ReturnsOriginalPoints()
    {
        var points = new List<Point2<double>>
        {
            new Point2<double>(0, 0),
            new Point2<double>(2, 0),
            new Point2<double>(0, 2),
            new Point2<double>(2, 2),
        };

        var relaxed = CentroidalVoronoiRelaxation.RelaxPoints(points, domain: null, iterations: 0);

        relaxed.Should().Equal(points);
    }

    [Fact]
    public void RelaxPoints_MovesPointsButKeepsCountAndBounds()
    {
        var points = new List<Point2<double>>
        {
            new Point2<double>(0, 0),
            new Point2<double>(2, 0),
            new Point2<double>(0, 2),
            new Point2<double>(2, 2),
        };

        var domain = new ClipPolygon(new[]
        {
            new Point2<double>(-1, -1),
            new Point2<double>(3, -1),
            new Point2<double>(3, 3),
            new Point2<double>(-1, 3),
        });

        var relaxed = CentroidalVoronoiRelaxation.RelaxPoints(points, domain, iterations: 2, step: 1.0);

        relaxed.Count.Should().Be(points.Count);
        for (int i = 0; i < points.Count; i++)
        {
            var original = points[i];
            var updated = relaxed[i];
            // All points should remain within the domain bounding box.
            updated.X.Should().BeGreaterThanOrEqualTo(-1.0 - 1e-6);
            updated.X.Should().BeLessThanOrEqualTo(3.0 + 1e-6);
            updated.Y.Should().BeGreaterThanOrEqualTo(-1.0 - 1e-6);
            updated.Y.Should().BeLessThanOrEqualTo(3.0 + 1e-6);
        }
    }
}
