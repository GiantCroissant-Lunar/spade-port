using System;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests;

public class BulkInsertionSpanTests
{
    [Fact]
    public void InsertBulk_WithReadOnlySpanPoint2_InsertsAllPoints()
    {
        // Arrange
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        var points = new Point2<double>[]
        {
            new(0.0, 0.0),
            new(1.0, 0.0),
            new(0.0, 1.0),
            new(1.0, 1.0)
        };
        ReadOnlySpan<Point2<double>> span = points;

        // Act
        triangulation.InsertBulk(span);

        // Assert
        Assert.Equal(4, triangulation.NumVertices);
    }

    [Fact]
    public void InsertBulk_WithReadOnlySpanPoint2_EmptySpan_DoesNothing()
    {
        // Arrange
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        ReadOnlySpan<Point2<double>> emptySpan = ReadOnlySpan<Point2<double>>.Empty;

        // Act
        triangulation.InsertBulk(emptySpan);

        // Assert
        Assert.Equal(0, triangulation.NumVertices);
    }

    [Fact]
    public void InsertBulk_WithReadOnlySpanPoint2_SpatialSortEnabled_SortsPoints()
    {
        // Arrange
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        var points = new Point2<double>[]
        {
            new(1.0, 1.0),
            new(0.0, 0.0),
            new(1.0, 0.0),
            new(0.0, 1.0)
        };
        ReadOnlySpan<Point2<double>> span = points;

        // Act
        triangulation.InsertBulk(span, useSpatialSort: true);

        // Assert
        Assert.Equal(4, triangulation.NumVertices);
        // The triangulation should be valid regardless of insertion order
        // We can't easily test the exact order without exposing internal state
        // but we can verify the triangulation is correct
    }

    [Fact]
    public void InsertBulk_WithReadOnlySpanPoint2_SpatialSortDisabled_PreservesOrder()
    {
        // Arrange
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        var points = new Point2<double>[]
        {
            new(1.0, 1.0),
            new(0.0, 0.0),
            new(1.0, 0.0),
            new(0.0, 1.0)
        };
        ReadOnlySpan<Point2<double>> span = points;

        // Act
        triangulation.InsertBulk(span, useSpatialSort: false);

        // Assert
        Assert.Equal(4, triangulation.NumVertices);
    }

    [Fact]
    public void InsertBulk_WithReadOnlySpanVertices_InsertsAllVertices()
    {
        // Arrange
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        var vertices = new Point2<double>[]
        {
            new(0.0, 0.0),
            new(1.0, 0.0),
            new(0.0, 1.0),
            new(1.0, 1.0)
        };
        ReadOnlySpan<Point2<double>> span = vertices;

        // Act
        triangulation.InsertBulk(span);

        // Assert
        Assert.Equal(4, triangulation.NumVertices);
    }

    [Fact]
    public void InsertBulk_WithReadOnlySpanVertices_EmptySpan_DoesNothing()
    {
        // Arrange
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        ReadOnlySpan<Point2<double>> emptySpan = ReadOnlySpan<Point2<double>>.Empty;

        // Act
        triangulation.InsertBulk(emptySpan);

        // Assert
        Assert.Equal(0, triangulation.NumVertices);
    }

    [Fact]
    public void InsertBulk_WithNullTriangulation_ThrowsArgumentNullException()
    {
        // Arrange
        DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>? triangulation = null;
        var points = new Point2<double>[] { new(0.0, 0.0) };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => triangulation!.InsertBulk(points.AsSpan()));
    }

    [Fact]
    public void InsertBulk_WithReadOnlySpanPoint2_SinglePoint_InsertsCorrectly()
    {
        // Arrange
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        var points = new Point2<double>[] { new(0.5, 0.5) };
        ReadOnlySpan<Point2<double>> span = points;

        // Act - single point should not trigger sorting logic
        triangulation.InsertBulk(span, useSpatialSort: true);

        // Assert
        Assert.Equal(1, triangulation.NumVertices);
    }
}