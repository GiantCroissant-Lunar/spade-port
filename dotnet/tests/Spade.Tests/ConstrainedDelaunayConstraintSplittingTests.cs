using System.Linq;
using FluentAssertions;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests;

public class ConstrainedDelaunayConstraintSplittingTests
{
    [Fact]
    public void AddConstraintWithSplitting_InsertsSteinerVertexNearIntersection()
    {
        var cdt = new ConstrainedDelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

        // Square vertices
        var v0 = cdt.Insert(new Point2<double>(0.0, 0.0));
        var v1 = cdt.Insert(new Point2<double>(1.0, 0.0));
        var v2 = cdt.Insert(new Point2<double>(0.0, 1.0));
        var v3 = cdt.Insert(new Point2<double>(1.0, 1.0));

        var beforeVertices = cdt.NumVertices;

        // First constraint: diagonal v0-v3
        cdt.AddConstraint(v0, v3).Should().BeTrue();

        // Second constraint: other diagonal v1-v2, using splitting
        var added = cdt.AddConstraintWithSplitting(
            v1,
            v2,
            p => new Point2<double>(p.X, p.Y));

        added.Should().BeTrue();
        cdt.NumVertices.Should().BeGreaterThan(beforeVertices);

        // There should now be a Steiner vertex near the intersection (0.5, 0.5)
        var intersection = new Point2<double>(0.5, 0.5);
        var hasSteiner = cdt.Vertices()
            .Select(v => v.Data)
            .Any(p =>
            {
                var dx = p.X - intersection.X;
                var dy = p.Y - intersection.Y;
                return dx * dx + dy * dy < 1e-3;
            });

        hasSteiner.Should().BeTrue();
    }

    [Fact]
    public void AddConstraintWithSplitting_UsesOppositeVertexWhenSplitPositionNearOpposite()
    {
        var cdt = new ConstrainedDelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

        var v0 = cdt.Insert(new Point2<double>(0.0, 0.0));
        var v1 = cdt.Insert(new Point2<double>(1.0, 0.0));
        var v2 = cdt.Insert(new Point2<double>(0.0, 1.0));
        var v3 = cdt.Insert(new Point2<double>(1.0, 1.0));

        cdt.AddConstraint(v0, v3).Should().BeTrue();

        var diag = cdt.GetEdgeFromNeighbors(v0, v3);
        diag.Should().NotBeNull();
        diag!.Value.AsUndirected().Data.IsConstraintEdge.Should().BeTrue();

        var added = cdt.AddConstraintWithSplitting(
            v1,
            v2,
            _ => new Point2<double>(1.0, 0.0));

        added.Should().BeTrue();

        diag = cdt.GetEdgeFromNeighbors(v0, v3);
        if (diag != null)
        {
            diag.Value.AsUndirected().Data.IsConstraintEdge.Should().BeFalse();
        }

        var e01 = cdt.GetEdgeFromNeighbors(v0, v1);
        e01.Should().NotBeNull();
        e01!.Value.AsUndirected().Data.IsConstraintEdge.Should().BeTrue();

        var e13 = cdt.GetEdgeFromNeighbors(v1, v3);
        e13.Should().NotBeNull();
        e13!.Value.AsUndirected().Data.IsConstraintEdge.Should().BeTrue();
    }

    [Fact]
    public void AddConstraintWithSplitting_NearEndpointIntersectionInsertsSteinerVertex()
    {
        var cdt = new ConstrainedDelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

        var a = cdt.Insert(new Point2<double>(0.0, 0.0));
        var b = cdt.Insert(new Point2<double>(5.0, 0.0));

        cdt.Insert(new Point2<double>(0.0, 1.0));
        cdt.Insert(new Point2<double>(5.0, 1.0));

        cdt.AddConstraint(a, b).Should().BeTrue();

        const double eps = 1e-9;
        const double factor = 1.0;

        var c = cdt.Insert(new Point2<double>(eps, 1.0));
        var d = cdt.Insert(new Point2<double>(eps * (1.0 + factor), -1.0));

        var beforeVertices = cdt.NumVertices;

        var added = cdt.AddConstraintWithSplitting(
            c,
            d,
            p => new Point2<double>(p.X, p.Y));

        added.Should().BeTrue();
        cdt.NumVertices.Should().Be(beforeVertices + 1);

        var aPos = new Point2<double>(0.0, 0.0);

        var steiner = cdt.Vertices()
            .Select(v => v.Data)
            .Where(p => System.Math.Abs(p.Y) < 1e-6 && !(p.X == 0.0 && p.Y == 0.0) && !(p.X == 5.0 && p.Y == 0.0))
            .OrderBy(p =>
            {
                var dx = p.X - aPos.X;
                var dy = p.Y - aPos.Y;
                return dx * dx + dy * dy;
            })
            .First();

        steiner.Y.Should().BeApproximately(0.0, 1e-6);
        steiner.X.Should().BeGreaterThan(0.0);
        steiner.X.Should().BeLessThan(0.01);
    }

    [Fact]
    public void AddConstraintWithSplitting_EpsilonNearOppositeVertexStillUsesOppositeVertex()
    {
        var cdt = new ConstrainedDelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

        var v0 = cdt.Insert(new Point2<double>(0.0, 0.0));
        var v1 = cdt.Insert(new Point2<double>(1.0, 0.0));
        var v2 = cdt.Insert(new Point2<double>(0.0, 1.0));
        var v3 = cdt.Insert(new Point2<double>(1.0, 1.0));

        cdt.AddConstraint(v0, v3).Should().BeTrue();

        var diag = cdt.GetEdgeFromNeighbors(v0, v3);
        diag.Should().NotBeNull();
        diag!.Value.AsUndirected().Data.IsConstraintEdge.Should().BeTrue();

        var beforeVertices = cdt.NumVertices;

        const double eps = 1e-8;

        var added = cdt.AddConstraintWithSplitting(
            v1,
            v2,
            _ => new Point2<double>(1.0 + eps, 0.0));

        added.Should().BeTrue();
        cdt.NumVertices.Should().Be(beforeVertices);

        diag = cdt.GetEdgeFromNeighbors(v0, v3);
        if (diag != null)
        {
            diag.Value.AsUndirected().Data.IsConstraintEdge.Should().BeFalse();
        }

        var e01 = cdt.GetEdgeFromNeighbors(v0, v1);
        e01.Should().NotBeNull();
        e01!.Value.AsUndirected().Data.IsConstraintEdge.Should().BeTrue();

        var e13 = cdt.GetEdgeFromNeighbors(v1, v3);
        e13.Should().NotBeNull();
        e13!.Value.AsUndirected().Data.IsConstraintEdge.Should().BeTrue();
    }
}
