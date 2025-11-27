using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Spade;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests.Interpolation;

public class ThreadSafeNaturalNeighborTests
{
    private readonly struct PointWithHeight : IHasPosition<double>
    {
        public Point2<double> Position { get; }
        public double Height { get; }

        public PointWithHeight(Point2<double> position, double height)
        {
            Position = position;
            Height = height;
        }
    }

    [Fact]
    public void ThreadSafeNaturalNeighbor_MatchesNonThreadSafe_ForScalarInterpolation()
    {
        var triangulation = new DelaunayTriangulation<PointWithHeight, int, int, int, LastUsedVertexHintGenerator<double>>();

        var coords = new[] { -1.0, -0.5, 0.0, 0.5, 1.0 };
        foreach (var x in coords)
        {
            foreach (var y in coords)
            {
                var p = new Point2<double>(x, y);
                triangulation.Insert(new PointWithHeight(p, x));
            }
        }

        var nn = triangulation.NaturalNeighbor();
        var ts = triangulation.ThreadSafeNaturalNeighbor();

        var queries = new[]
        {
            new Point2<double>(-0.8, -0.2),
            new Point2<double>(-0.3, 0.7),
            new Point2<double>(0.4, 0.0),
            new Point2<double>(0.9, -0.1)
        };

        foreach (var q in queries)
        {
            var v1 = nn.Interpolate(v => ((PointWithHeight)v.Data).Height, q);
            var v2 = ts.Interpolate(v => ((PointWithHeight)v.Data).Height, q);

            v1.Should().NotBeNull();
            v2.Should().NotBeNull();
            v1!.Value.Should().BeApproximately(v2!.Value, 1e-9);
        }
    }

    [Fact]
    public async Task ThreadSafeNaturalNeighbor_SupportsConcurrentQueries()
    {
        var triangulation = new DelaunayTriangulation<PointWithHeight, int, int, int, LastUsedVertexHintGenerator<double>>();

        var coords = new[] { -1.0, -0.5, 0.0, 0.5, 1.0 };
        foreach (var x in coords)
        {
            foreach (var y in coords)
            {
                var p = new Point2<double>(x, y);
                triangulation.Insert(new PointWithHeight(p, x));
            }
        }

        var ts = triangulation.ThreadSafeNaturalNeighbor();

        var queries = new List<Point2<double>>
        {
            new(-0.8, -0.2),
            new(-0.3, 0.7),
            new(0.4, 0.0),
            new(0.9, -0.1),
            new(0.0, 0.0),
            new(0.2, 0.3),
        };

        var tasks = new List<Task>();

        foreach (var q in queries)
        {
            tasks.Add(Task.Run(() =>
            {
                var value = ts.Interpolate(v => ((PointWithHeight)v.Data).Height, q);
                value.Should().NotBeNull();
                value!.Value.Should().BeApproximately(q.X, 1e-2);
            }));
        }

        await Task.WhenAll(tasks);
    }
}
