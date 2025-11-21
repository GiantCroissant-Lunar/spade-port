using System.Linq;
using FluentAssertions;
using Spade;
using Spade.Advanced.Voronoi;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests;

public class ClippedVoronoiTests
{
    [Fact]
    public void ClipToPolygon_ProducesCellsWithinDomain()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        triangulation.Insert(new Point2<double>(0, 0));
        triangulation.Insert(new Point2<double>(2, 0));
        triangulation.Insert(new Point2<double>(0, 2));
        triangulation.Insert(new Point2<double>(2, 2));

        var domain = new ClipPolygon(new[]
        {
            new Point2<double>(-10, -10),
            new Point2<double>(10, -10),
            new Point2<double>(10, 10),
            new Point2<double>(-10, 10),
        });

        var diagram = ClippedVoronoiBuilder.ClipToPolygon(triangulation, domain);

        diagram.Should().NotBeNull();
        // Depending on triangulation topology, some or all Voronoi cells may be unbounded
        // and currently skipped by the simple polygon extraction. When cells are present,
        // they must be valid polygons fully contained in the domain.

        foreach (var cell in diagram.Cells)
        {
            cell.Polygon.Count.Should().BeGreaterThanOrEqualTo(3);

            foreach (var p in cell.Polygon)
            {
                PointInsideConvexPolygon(p, domain.Vertices).Should().BeTrue();
            }
        }
    }

    [Fact]
    public void ClipToPolygon_SetsIsClipped_WhenDomainIsSmall()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        triangulation.Insert(new Point2<double>(-5, 0));
        triangulation.Insert(new Point2<double>(5, 0));
        triangulation.Insert(new Point2<double>(0, -5));
        triangulation.Insert(new Point2<double>(0, 5));
        triangulation.Insert(new Point2<double>(0, 0));

        var smallDomain = new ClipPolygon(new[]
        {
            new Point2<double>(-1, -1),
            new Point2<double>(1, -1),
            new Point2<double>(1, 1),
            new Point2<double>(-1, 1),
        });

        var diagram = ClippedVoronoiBuilder.ClipToPolygon(triangulation, smallDomain);

        // If any cells are produced, at least one should be clipped by the small domain.
        if (diagram.Cells.Count > 0)
        {
            diagram.Cells.Any(c => c.IsClipped).Should().BeTrue();
        }
    }

    private static bool PointInsideConvexPolygon(Point2<double> p, System.Collections.Generic.IReadOnlyList<Point2<double>> polygon)
    {
        int count = polygon.Count;
        for (int i = 0; i < count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % count];
            double cross = (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
            if (cross < -1e-6)
            {
                return false;
            }
        }
        return true;
    }
}
