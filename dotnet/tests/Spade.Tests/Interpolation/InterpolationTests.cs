using System.Collections.Generic;
using FluentAssertions;
using Spade;
using Spade.Handles;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests.Interpolation;

public class InterpolationTests
{
    [Fact]
    public void Barycentric_InsideTriangle_ReturnsThreeWeightsSummingToOne()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

        triangulation.Insert(new Point2<double>(0.0, 0.0));
        triangulation.Insert(new Point2<double>(1.0, 0.0));
        triangulation.Insert(new Point2<double>(0.0, 1.0));

        var bary = triangulation.Barycentric();
        var weights = new List<(FixedVertexHandle Vertex, double Weight)>();

        var query = new Point2<double>(1.0 / 3.0, 1.0 / 3.0);
        bary.GetWeights(query, weights);

        weights.Count.Should().Be(3);
        var sum = 0.0;
        foreach (var (_, w) in weights)
        {
            sum += w;
        }

        sum.Should().BeApproximately(1.0, 1e-9);
    }

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
    public void NaturalNeighbor_ConstantField_ReturnsConstantInsideHull()
    {
        var triangulation = new DelaunayTriangulation<PointWithHeight, int, int, int, LastUsedVertexHintGenerator<double>>();

        var h = 1.5;
        triangulation.Insert(new PointWithHeight(new Point2<double>(-1.0, -1.0), h));
        triangulation.Insert(new PointWithHeight(new Point2<double>(-1.0, 1.0), h));
        triangulation.Insert(new PointWithHeight(new Point2<double>(1.0, -1.0), h));
        triangulation.Insert(new PointWithHeight(new Point2<double>(1.0, 1.0), h));

        var nn = triangulation.NaturalNeighbor();
        var value = nn.Interpolate(v => ((PointWithHeight)v.Data).Height, new Point2<double>(0.0, 0.0));

        value.Should().NotBeNull();
        value!.Value.Should().BeApproximately(h, 1e-6);
    }

    [Fact]
    public void NaturalNeighbor_SymmetricTriangle_HasSymmetricWeights()
    {
        var triangulation = new DelaunayTriangulation<PointWithHeight, int, int, int, LastUsedVertexHintGenerator<double>>();

        var top = triangulation.Insert(new PointWithHeight(new Point2<double>(0.0, System.Math.Sqrt(2.0)), 0.0));
        var v1 = triangulation.Insert(new PointWithHeight(new Point2<double>(-1.0, -1.0), 0.0));
        var v2 = triangulation.Insert(new PointWithHeight(new Point2<double>(1.0, -1.0), 0.0));

        var nn = triangulation.NaturalNeighbor();
        var weights = new List<(FixedVertexHandle Vertex, double Weight)>();

        nn.GetWeights(new Point2<double>(0.0, 0.0), weights);

        weights.Count.Should().Be(3);

        double? w1 = null;
        double? w2 = null;
        foreach (var (vertex, weight) in weights)
        {
            if (vertex == v1)
            {
                w1 = weight;
            }
            else if (vertex == v2)
            {
                w2 = weight;
            }

            weight.Should().BeGreaterThan(0.0);
        }

        w1.Should().NotBeNull();
        w2.Should().NotBeNull();
        w1!.Value.Should().BeApproximately(w2!.Value, 1e-9);
    }

    [Fact]
    public void NaturalNeighbor_UnitSquare_CenterHasEqualWeights()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

        triangulation.Insert(new Point2<double>(1.0, 1.0));
        triangulation.Insert(new Point2<double>(1.0, -1.0));
        triangulation.Insert(new Point2<double>(-1.0, 1.0));
        triangulation.Insert(new Point2<double>(-1.0, -1.0));

        var nn = triangulation.NaturalNeighbor();
        var weights = new List<(FixedVertexHandle Vertex, double Weight)>();

        nn.GetWeights(new Point2<double>(0.0, 0.0), weights);

        weights.Count.Should().Be(4);
        foreach (var (_, weight) in weights)
        {
            weight.Should().BeApproximately(0.25, 1e-9);
        }
    }

    [Fact]
    public void NaturalNeighbor_SlopeField_MatchesExpectedXWithinHull()
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

        var insideQueries = new[]
        {
            new Point2<double>(-0.8, -0.2),
            new Point2<double>(-0.3, 0.7),
            new Point2<double>(0.4, 0.0),
            new Point2<double>(0.9, -0.1)
        };

        foreach (var q in insideQueries)
        {
            var value = nn.Interpolate(v => ((PointWithHeight)v.Data).Height, q);
            value.Should().NotBeNull();
            value!.Value.Should().BeApproximately(q.X, 1e-2);
        }

        var outsideQueries = new[]
        {
            new Point2<double>(-2.0, 0.0),
            new Point2<double>(2.0, 0.5)
        };

        foreach (var q in outsideQueries)
        {
            var value = nn.Interpolate(v => ((PointWithHeight)v.Data).Height, q);
            value.Should().BeNull();
        }
    }
}
