using System.Linq;
using System.Reflection;
using FluentAssertions;
using Spade;
using Spade.DCEL;
using Spade.Handles;
using Spade.Primitives;
using Spade.Refinement;
using Xunit;

namespace Spade.Tests.Refinement;

public class MeshRefinementTests
{
    [Fact]
    public void Refine_WithoutMaxArea_IsNoop()
    {
        var cdt = new ConstrainedDelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

        cdt.Insert(new Point2<double>(0.0, 0.0));
        cdt.Insert(new Point2<double>(1.0, 0.0));
        cdt.Insert(new Point2<double>(0.0, 1.0));

        var beforeVertices = cdt.NumVertices;
        var beforeFaces = cdt.NumFaces;

        var parameters = new RefinementParameters();

        var result = cdt.Refine(parameters);

        result.AddedVertices.Should().Be(0);
        result.ReachedVertexLimit.Should().BeFalse();
        cdt.NumVertices.Should().Be(beforeVertices);
        cdt.NumFaces.Should().Be(beforeFaces);
    }

    [Fact]
    public void Refine_WithMaxArea_SplitsLargeTriangle()
    {
        var cdt = new ConstrainedDelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

        cdt.Insert(new Point2<double>(0.0, 0.0));
        cdt.Insert(new Point2<double>(10.0, 0.0));
        cdt.Insert(new Point2<double>(0.0, 10.0));

        var beforeVertices = cdt.NumVertices;

        // Capture the initial maximum inner-face area as a baseline.
        var initialAreas = cdt.InnerFaces()
            .Select(face => ComputeTriangleArea(cdt, face))
            .ToList();
        initialAreas.Should().NotBeEmpty();
        var initialMaxArea = initialAreas.Max();

        var parameters = new RefinementParameters
        {
            MaxAllowedArea = 5.0,
            MaxAdditionalVertices = 50
        };

        var result = cdt.Refine(parameters);

        result.AddedVertices.Should().BeGreaterThan(0);
        cdt.NumVertices.Should().BeGreaterThan(beforeVertices);

        // Verify that refinement has actually reduced the worst-case triangle area.
        var areas = cdt.InnerFaces()
            .Select(face => ComputeTriangleArea(cdt, face))
            .ToList();

        areas.Should().NotBeEmpty();
        var refinedMaxArea = areas.Max();
        refinedMaxArea.Should().BeLessThan(initialMaxArea);
    }

    [Fact]
    public void Refine_WithAngleLimit_RefinesEvenWithoutAreaConstraint()
    {
        var cdt = new ConstrainedDelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

        // Construct a skinny triangle with a very small angle at the third vertex.
        cdt.Insert(new Point2<double>(0.0, 0.0));
        cdt.Insert(new Point2<double>(10.0, 0.0));
        cdt.Insert(new Point2<double>(0.5, 0.01));

        var beforeVertices = cdt.NumVertices;

        var parameters = new RefinementParameters
        {
            MaxAdditionalVertices = 50
        };
        parameters.WithAngleLimit(AngleLimit.FromDegrees(20.0));

        var result = cdt.Refine(parameters);

        result.AddedVertices.Should().BeGreaterThan(0);
        cdt.NumVertices.Should().BeGreaterThan(beforeVertices);
    }

    [Fact]
    public void IsEncroachingEdge_HasExpectedDiametralCircleSemantics()
    {
        var method = typeof(MeshRefinementExtensions)
            .GetMethod("IsEncroachingEdge", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        var edgeFrom = new Point2<double>(0.0, 0.0);
        var edgeTo = new Point2<double>(2.0, 0.0);

        var inside = (bool)method!.Invoke(null, new object[] { edgeFrom, edgeTo, new Point2<double>(1.0, 0.0) })!;
        inside.Should().BeTrue();

        var onBoundary = (bool)method!.Invoke(null, new object[] { edgeFrom, edgeTo, new Point2<double>(1.0, 1.0) })!;
        onBoundary.Should().BeFalse();

        var outside = (bool)method!.Invoke(null, new object[] { edgeFrom, edgeTo, new Point2<double>(1.0, 1.1) })!;
        outside.Should().BeFalse();
    }

    [Fact]
    public void IsFixedEdge_DetectsHullButNotInteriorEdges()
    {
        var cdt = new ConstrainedDelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

        cdt.Insert(new Point2<double>(0.0, 0.0));
        cdt.Insert(new Point2<double>(2.0, 0.0));
        cdt.Insert(new Point2<double>(0.0, 2.0));
        cdt.Insert(new Point2<double>(2.0, 2.0));

        var methodInfo = typeof(MeshRefinementExtensions)
            .GetMethod("IsFixedEdge", BindingFlags.NonPublic | BindingFlags.Static);
        methodInfo.Should().NotBeNull();

        methodInfo = methodInfo!.MakeGenericMethod(
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(LastUsedVertexHintGenerator<double>));

        var hullEdges = new System.Collections.Generic.List<FixedUndirectedEdgeHandle>();
        var interiorEdges = new System.Collections.Generic.List<FixedUndirectedEdgeHandle>();

        foreach (var undirected in cdt.UndirectedEdges())
        {
            var fixedEdge = undirected.Handle;
            var e0 = cdt.DirectedEdge(new FixedDirectedEdgeHandle(fixedEdge.Index * 2));
            var e1 = e0.Rev();

            var isHull = e0.IsOuterEdge() || e1.IsOuterEdge();
            if (isHull)
            {
                hullEdges.Add(fixedEdge);
            }
            else
            {
                interiorEdges.Add(fixedEdge);
            }
        }

        hullEdges.Should().NotBeEmpty();
        interiorEdges.Should().NotBeEmpty();

        foreach (var edge in hullEdges)
        {
            var undirected = cdt.UndirectedEdge(edge);
            var result = (bool)methodInfo!.Invoke(null, new object[] { cdt, undirected })!;
            result.Should().BeTrue();
        }

        foreach (var edge in interiorEdges)
        {
            var undirected = cdt.UndirectedEdge(edge);
            var result = (bool)methodInfo!.Invoke(null, new object[] { cdt, undirected })!;
            result.Should().BeFalse();
        }
    }

    [Fact]
    public void Refine_WithConstraintEncroachment_SplitsConstraintAndStopsRefining()
    {
        var cdt = new ConstrainedDelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

        var v0 = cdt.Insert(new Point2<double>(0.0, 0.0));
        var v1 = cdt.Insert(new Point2<double>(2.0, 0.0));
        var v2 = cdt.Insert(new Point2<double>(1.0, 1.5));

        var initialVertices = cdt.NumVertices;

        var added = cdt.AddConstraint(v0, v1);
        added.Should().BeTrue();
        cdt.NumConstraints.Should().Be(1);

        var parameters = new RefinementParameters
        {
            MaxAllowedArea = 1.0,
            MaxAdditionalVertices = 10
        };

        var result = cdt.Refine(parameters);

        result.AddedVertices.Should().Be(0);
        result.ReachedVertexLimit.Should().BeFalse();
        cdt.NumVertices.Should().Be(initialVertices + 1);
        cdt.NumConstraints.Should().BeGreaterThan(0);

        var hasMidpoint = cdt.Vertices()
            .Select(v => v.Data.Position)
            .Any(p => Math.Abs(p.X - 1.0) < 1e-9 && Math.Abs(p.Y - 0.0) < 1e-9);

        hasMidpoint.Should().BeTrue();
    }

    [Fact]
    public void ResolveEncroachment_SplitsConstraintEdgeAtMidpoint()
    {
        var cdt = new ConstrainedDelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

        var v0 = cdt.Insert(new Point2<double>(0.0, 0.0));
        var v1 = cdt.Insert(new Point2<double>(2.0, 0.0));
        var v2 = cdt.Insert(new Point2<double>(1.0, 1.5));

        var initialVertices = cdt.NumVertices;

        var added = cdt.AddConstraint(v0, v1);
        added.Should().BeTrue();
        cdt.NumConstraints.Should().BeGreaterThan(0);

        var constraintEdge = cdt
            .UndirectedEdges()
            .First(e => e.Data.IsConstraintEdge)
            .Handle;

        var methodInfo = typeof(MeshRefinementExtensions)
            .GetMethod("ResolveEncroachment", BindingFlags.NonPublic | BindingFlags.Static);
        methodInfo.Should().NotBeNull();

        methodInfo = methodInfo!.MakeGenericMethod(
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(LastUsedVertexHintGenerator<double>));

        var segmentBuffer = new System.Collections.Generic.Queue<FixedUndirectedEdgeHandle>();
        var faceBuffer = new System.Collections.Generic.Queue<FaceHandle<Point2<double>, int, CdtEdge<int>, int>>();

        methodInfo.Invoke(null, new object[] { cdt, segmentBuffer, faceBuffer, constraintEdge });

        cdt.NumVertices.Should().Be(initialVertices + 1);

        var hasMidpoint = cdt.Vertices()
            .Select(v => v.Data.Position)
            .Any(p => Math.Abs(p.X - 1.0) < 1e-9 && Math.Abs(p.Y - 0.0) < 1e-9);

        hasMidpoint.Should().BeTrue();
    }

    private static double ComputeTriangleArea(
        ConstrainedDelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>> cdt,
        FaceHandle<Point2<double>, int, CdtEdge<int>, int> face)
    {
        var edge = face.AdjacentEdge() ?? throw new System.InvalidOperationException("Face has no adjacent edge");

        var v0 = edge.From().Data.Position;
        var v1 = edge.To().Data.Position;
        var v2 = edge.Next().To().Data.Position;

        var ax = v1.X - v0.X;
        var ay = v1.Y - v0.Y;
        var bx = v2.X - v0.X;
        var by = v2.Y - v0.Y;

        var cross = ax * by - ay * bx;
        return System.Math.Abs(cross) * 0.5;
    }
}
