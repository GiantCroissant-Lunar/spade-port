using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Spade.Handles;
using Spade.Primitives;
using Xunit;

using IntersectionD = Spade.Intersection<Spade.Primitives.Point2<double>, int, int, int>;

namespace Spade.Tests;

public class LineIntersectionIteratorTests
{
    private static DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>> CreateTestTriangulation(
        out FixedVertexHandle v0,
        out FixedVertexHandle v1,
        out FixedVertexHandle v2,
        out FixedVertexHandle v3)
    {
        var d = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        v0 = d.Insert(new Point2<double>(-2.0, -2.0));
        v1 = d.Insert(new Point2<double>(2.0, 2.0));
        v2 = d.Insert(new Point2<double>(1.0, -1.0));
        v3 = d.Insert(new Point2<double>(-1.0, 1.0));
        return d;
    }

    private static void Check(
        DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>> d,
        Point2<double> from,
        Point2<double> to,
        IList<IntersectionD> expected)
    {
        var collected = new LineIntersectionIterator<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>(d, from, to).ToList();
        collected.Should().Equal(expected);

        var revCollected = new LineIntersectionIterator<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>(d, to, from).ToList();
        var reversed = revCollected.Select(Reverse).ToList();
        var expectedReversed = expected.Reverse().ToList();
        reversed.Should().Equal(expectedReversed);
    }

    private static IntersectionD Reverse(IntersectionD intersection)
    {
        return intersection switch
        {
            IntersectionD.EdgeIntersection e => new IntersectionD.EdgeIntersection(e.Edge.Rev()),
            IntersectionD.VertexIntersection v => new IntersectionD.VertexIntersection(v.Vertex),
            IntersectionD.EdgeOverlap e => new IntersectionD.EdgeOverlap(e.Edge.Rev()),
            _ => throw new InvalidOperationException("Unknown intersection type"),
        };
    }

    [Fact]
    public void Intersects_ZeroVertices_ReturnsEmpty()
    {
        var d = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        var iterator = new LineIntersectionIterator<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>(
            d,
            new Point2<double>(0.5, 1.234),
            new Point2<double>(3.223, 42.0));

        var intersections = iterator.ToList();
        intersections.Should().BeEmpty();
    }

    [Fact]
    public void Intersects_SingleVertex_UsesNoTriangulationLogic()
    {
        var d = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        var pos = new Point2<double>(0.5, 0.5);
        var v0 = d.Insert(pos);

        var vertex = d.Vertex(v0);

        var from = new Point2<double>(1.0, 0.0);
        var to = new Point2<double>(0.0, 1.0);
        var intersections = new LineIntersectionIterator<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>(d, from, to).ToList();

        intersections.Should().HaveCount(1);
        intersections[0].Should().BeOfType<IntersectionD.VertexIntersection>();
        var vInt = (IntersectionD.VertexIntersection)intersections[0];
        vInt.Vertex.Handle.Should().Be(v0);

        var intersectionsPoint = new LineIntersectionIterator<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>(d, pos, pos).ToList();
        intersectionsPoint.Should().HaveCount(1);
        intersectionsPoint[0].Should().BeOfType<IntersectionD.VertexIntersection>();
        var vInt2 = (IntersectionD.VertexIntersection)intersectionsPoint[0];
        vInt2.Vertex.Handle.Should().Be(v0);

        var toFar = new Point2<double>(1.234, 42.0);
        var intersectionsNone = new LineIntersectionIterator<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>(d, from, toFar).ToList();
        intersectionsNone.Should().BeEmpty();
    }

    [Fact]
    public void Intersects_OutsideOfConvexHull_MatchesReferenceScenario()
    {
        var d = CreateTestTriangulation(out var v0, out _, out _, out _);

        // 1) Line starting outside the convex hull that misses the hull entirely
        var noHitFrom = new Point2<double>(-3.0, 3.0);
        var noHitTo = new Point2<double>(-3.0, -3.0);
        var noHitIntersections = new LineIntersectionIterator<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>(d, noHitFrom, noHitTo).ToList();
        noHitIntersections.Should().BeEmpty();

        // 2) Line starting clearly outside the convex hull and entering it
        var outFrom = new Point2<double>(-3.0, 0.0);
        var outTo = new Point2<double>(0.0, 0.0);

        var location = d.LocateWithHintOptionCore(outFrom, null);
        location.Should().BeOfType<PositionInTriangulation.OutsideOfConvexHull>();

        var outIntersections = new LineIntersectionIterator<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>(d, outFrom, outTo).ToList();
        outIntersections.Should().NotBeEmpty();
        (outIntersections[0] is IntersectionD.EdgeIntersection ||
         outIntersections[0] is IntersectionD.VertexIntersection).Should().BeTrue();

        // 3) Line starting on the hull and leaving through vertex v0
        var fromHull = new Point2<double>(-2.0, 0.0);
        var toHull = new Point2<double>(-2.0, -4.0);
        var hullIntersections = new LineIntersectionIterator<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>(d, fromHull, toHull).ToList();

        hullIntersections.Should().NotBeEmpty();
        var vertexHits = hullIntersections.OfType<IntersectionD.VertexIntersection>().ToList();
        vertexHits.Should().NotBeEmpty();
        vertexHits.Select(v => v.Vertex.Handle).Should().Contain(v0);
    }
}
