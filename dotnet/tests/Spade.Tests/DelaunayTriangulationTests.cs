using FluentAssertions;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests;

public class DelaunayTriangulationTests
{
    [Fact]
    public void TestInsertFirst()
    {
        var d = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        d.Insert(new Point2<double>(0.0, 0.0));
        
        d.NumVertices.Should().Be(1);
        d.NumFaces.Should().Be(1); // Outer face
        d.NumUndirectedEdges.Should().Be(0);
    }

    [Fact]
    public void TestInsertSecond()
    {
        var d = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        d.Insert(new Point2<double>(0.0, 0.0));
        d.Insert(new Point2<double>(1.0, 1.0));
        
        d.NumVertices.Should().Be(2);
        d.NumFaces.Should().Be(1);
        d.NumUndirectedEdges.Should().Be(1);
    }

    [Fact]
    public void TestInsertThird()
    {
        var d = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        d.Insert(new Point2<double>(0.0, 0.0));
        d.Insert(new Point2<double>(1.0, 0.0));
        d.Insert(new Point2<double>(0.0, 1.0));
        
        d.NumVertices.Should().Be(3);
        d.NumFaces.Should().Be(2); // Outer + 1 inner
        d.NumUndirectedEdges.Should().Be(3);
    }

    [Fact]
    public void TestInsertFourthInside()
    {
        var d = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        d.Insert(new Point2<double>(0.0, 0.0));
        d.Insert(new Point2<double>(2.0, 0.0));
        d.Insert(new Point2<double>(0.0, 2.0));
        
        d.Insert(new Point2<double>(0.5, 0.5));
        
        d.NumVertices.Should().Be(4);
        d.NumFaces.Should().Be(4); // Outer + 3 inner
        d.NumUndirectedEdges.Should().Be(6);
    }

    [Fact]
    public void TestInsertFourthOutside()
    {
        var d = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        d.Insert(new Point2<double>(0.0, 0.0));
        d.Insert(new Point2<double>(1.0, 0.0));
        d.Insert(new Point2<double>(0.0, 1.0));
        
        d.Insert(new Point2<double>(1.0, 1.0)); // Makes a quad
        
        d.NumVertices.Should().Be(4);
        d.NumFaces.Should().Be(3); // Outer + 2 inner
        d.NumUndirectedEdges.Should().Be(5);
    }
}
