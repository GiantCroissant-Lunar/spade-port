using Spade.Handles;
using Spade.Primitives;
using Spade.Voronoi;
using Xunit;
using FluentAssertions;

namespace Spade.Tests;

public class VoronoiTests
{
    [Fact]
    public void TestVoronoiFaces()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        triangulation.Insert(new Point2<double>(0, 0));
        triangulation.Insert(new Point2<double>(1, 0));
        triangulation.Insert(new Point2<double>(0, 1));
        triangulation.Insert(new Point2<double>(1, 1));

        var faces = triangulation.VoronoiFaces().ToList();
        faces.Count.Should().Be(4);
    }

    [Fact]
    public void TestVoronoiVertexCircumcenter()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        triangulation.Insert(new Point2<double>(0, 0));
        triangulation.Insert(new Point2<double>(2, 0));
        triangulation.Insert(new Point2<double>(0, 2));
        // Triangle (0,0)-(2,0)-(0,2) is right isosceles.
        // Circumcenter should be at (1, 1).

        var innerFaces = triangulation.InnerFaces().ToList();
        innerFaces.Count.Should().Be(1);
        
        var face = innerFaces[0];
        var circumcenter = face.Circumcenter();
        circumcenter.X.Should().Be(1.0);
        circumcenter.Y.Should().Be(1.0);
        
        var voronoiVertex = new VoronoiVertex<Point2<double>, int, int, int>.Inner(face);
        voronoiVertex.Position.Should().Be(new Point2<double>(1.0, 1.0));
    }

    [Fact]
    public void TestVoronoiEdgeDirection()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        triangulation.Insert(new Point2<double>(0, 0));
        triangulation.Insert(new Point2<double>(2, 0));
        
        var edge = triangulation.DirectedEdges().First(e => e.From().Data.Position == new Point2<double>(0, 0) && e.To().Data.Position == new Point2<double>(2, 0));
        var voronoiEdge = new DirectedVoronoiEdge<Point2<double>, int, int, int>(edge);
        
        var dir = voronoiEdge.DirectionVector();
        // Delaunay edge is (2, 0). Rotated 90 degrees CCW is (0, 2).
        // Implementation: (-diff.Y, diff.X)
        // diff = (2, 0). -diff.Y = 0, diff.X = 2. Result (0, 2).
        
        dir.X.Should().Be(0);
        dir.Y.Should().Be(2);
    }
}
