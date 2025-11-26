using FluentAssertions;
using Spade;
using Spade.Handles;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests.Interpolation;

public class GradientInterpolationTests
{
    private readonly struct PointWithHeightAndGrad : IHasPosition<double>
    {
        public Point2<double> Position { get; }
        public double Height { get; }
        public Point2<double> Grad { get; }

        public PointWithHeightAndGrad(Point2<double> position, double height, Point2<double> grad)
        {
            Position = position;
            Height = height;
            Grad = grad;
        }
    }

    [Fact]
    public void EstimateGradient_PlanarField_RecoversExpectedGradient()
    {
        var tri = new DelaunayTriangulation<PointWithHeightAndGrad, int, int, int, LastUsedVertexHintGenerator<double>>();

        // Plane: h(x,y) = y, so gradient is (0, 1)
        tri.Insert(new PointWithHeightAndGrad(new Point2<double>(0.0, 0.0), 0.0, new Point2<double>(0.0, 1.0)));
        tri.Insert(new PointWithHeightAndGrad(new Point2<double>(1.0, 0.0), 0.0, new Point2<double>(0.0, 1.0)));
        tri.Insert(new PointWithHeightAndGrad(new Point2<double>(1.0, 1.0), 1.0, new Point2<double>(0.0, 1.0)));
        tri.Insert(new PointWithHeightAndGrad(new Point2<double>(0.0, 1.0), 1.0, new Point2<double>(0.0, 1.0)));

        var nn = tri.NaturalNeighbor();
        var g = nn.EstimateGradients(v => ((PointWithHeightAndGrad)v.Data).Height);

        foreach (var v in tri.Vertices())
        {
            var grad = g(v);
            grad.X.Should().BeApproximately(0.0, 1e-6);
            grad.Y.Should().BeApproximately(1.0, 1e-6);
        }
    }

    [Fact]
    public void InterpolateGradient_ConstantFieldWithZeroGradient_EqualsConstant()
    {
        var tri = new DelaunayTriangulation<PointWithHeightAndGrad, int, int, int, LastUsedVertexHintGenerator<double>>();

        var h = 2.0;
        var zero = new Point2<double>(0.0, 0.0);

        tri.Insert(new PointWithHeightAndGrad(new Point2<double>(-1.0, -1.0), h, zero));
        tri.Insert(new PointWithHeightAndGrad(new Point2<double>(-1.0, 1.0), h, zero));
        tri.Insert(new PointWithHeightAndGrad(new Point2<double>(1.0, -1.0), h, zero));
        tri.Insert(new PointWithHeightAndGrad(new Point2<double>(1.0, 1.0), h, zero));

        var nn = tri.NaturalNeighbor();
        var value = nn.InterpolateGradient(
            v => ((PointWithHeightAndGrad)v.Data).Height,
            v => ((PointWithHeightAndGrad)v.Data).Grad,
            flatness: 1.0,
            position: new Point2<double>(0.2, -0.3));

        value.Should().NotBeNull();
        value!.Value.Should().BeApproximately(h, 1e-6);
    }

    [Fact]
    public void InterpolateGradient_SlopeField_UsesProvidedGradients()
    {
        var tri = new DelaunayTriangulation<PointWithHeightAndGrad, int, int, int, LastUsedVertexHintGenerator<double>>();

        var gradX = new Point2<double>(1.0, 0.0);
        var coords = new[] { -1.0, -0.5, 0.0, 0.5, 1.0 };
        foreach (var x in coords)
        {
            foreach (var y in coords)
            {
                var p = new Point2<double>(x, y);
                tri.Insert(new PointWithHeightAndGrad(p, x, gradX));
            }
        }

        var nn = tri.NaturalNeighbor();
        var queries = new[]
        {
            new Point2<double>(-0.8, -0.2),
            new Point2<double>(-0.1, 0.6),
            new Point2<double>(0.7, -0.3)
        };

        foreach (var q in queries)
        {
            var value = nn.InterpolateGradient(
                v => ((PointWithHeightAndGrad)v.Data).Height,
                v => ((PointWithHeightAndGrad)v.Data).Grad,
                flatness: 1.0,
                position: q);

            value.Should().NotBeNull();
            value!.Value.Should().BeApproximately(q.X, 1e-2);
        }
    }

    [Fact]
    public void InterpolateGradient_UsingEstimatedGradients_MatchesPlanarField()
    {
        var tri = new DelaunayTriangulation<PointWithHeightAndGrad, int, int, int, LastUsedVertexHintGenerator<double>>();

        var coords = new[] { -1.0, -0.5, 0.0, 0.5, 1.0 };
        foreach (var x in coords)
        {
            foreach (var y in coords)
            {
                var p = new Point2<double>(x, y);
                tri.Insert(new PointWithHeightAndGrad(p, y, default));
            }
        }

        var nn = tri.NaturalNeighbor();
        var grads = nn.EstimateGradients(v => ((PointWithHeightAndGrad)v.Data).Height);

        var queries = new[]
        {
            new Point2<double>(-0.8, -0.1),
            new Point2<double>(0.0, 0.4),
            new Point2<double>(0.6, 0.9)
        };

        foreach (var q in queries)
        {
            var value = nn.InterpolateGradient(
                v => ((PointWithHeightAndGrad)v.Data).Height,
                grads,
                flatness: 1.0,
                position: q);

            value.Should().NotBeNull();
            value!.Value.Should().BeApproximately(q.Y, 5e-2);
        }
    }
}
