using System;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests;

/// <summary>
/// Demonstration tests showing the new span-based bulk insertion APIs in action.
/// These tests verify that the new APIs work correctly and provide the expected functionality.
/// </summary>
public class BulkInsertionDemoTests
{
    [Fact]
    public void Demo_SpanBasedBulkInsertion_WithPoint2Double()
    {
        // Arrange - Create a triangulation for Point2<double> vertices
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        
        // Create an array of points
        var points = new Point2<double>[]
        {
            new(0.0, 0.0),
            new(2.0, 0.0),
            new(1.0, 2.0),
            new(3.0, 1.0),
            new(0.5, 1.5)
        };

        // Act - Use the new span-based API for zero-allocation insertion
        ReadOnlySpan<Point2<double>> span = points;
        triangulation.InsertBulk(span, useSpatialSort: true);

        // Assert - Verify all points were inserted
        Assert.Equal(5, triangulation.NumVertices);
        Assert.True(triangulation.NumFaces > 1); // Should have created triangular faces
        Assert.True(triangulation.NumUndirectedEdges > 0); // Should have created edges
    }

    [Fact]
    public void Demo_SpanBasedBulkInsertion_WithCustomVertexType()
    {
        // Arrange - Create a triangulation for custom vertex type
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        
        // Create an array of custom vertices (Point2<double> implements IHasPosition<double>)
        var vertices = new Point2<double>[]
        {
            new(0.0, 0.0),
            new(1.0, 0.0),
            new(0.0, 1.0),
            new(1.0, 1.0)
        };

        // Act - Use the generic span-based API
        ReadOnlySpan<Point2<double>> span = vertices;
        triangulation.InsertBulk(span, useSpatialSort: false); // Preserve insertion order

        // Assert - Verify all vertices were inserted
        Assert.Equal(4, triangulation.NumVertices);
    }

    [Fact]
    public void Demo_PreallocationBehavior_LargerDataset()
    {
        // Arrange - Create a larger dataset to demonstrate preallocation benefits
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        
        // Generate a grid of points
        var points = new Point2<double>[100];
        int index = 0;
        for (int x = 0; x < 10; x++)
        {
            for (int y = 0; y < 10; y++)
            {
                points[index++] = new Point2<double>(x * 0.1, y * 0.1);
            }
        }

        // Act - The new API should preallocate capacity automatically
        ReadOnlySpan<Point2<double>> span = points;
        triangulation.InsertBulk(span, useSpatialSort: true);

        // Assert - Verify all points were inserted and triangulation is valid
        Assert.Equal(100, triangulation.NumVertices);
        
        // For a 10x10 grid, we expect a substantial number of triangular faces
        // The exact number depends on the triangulation algorithm, but should be > 150
        Assert.True(triangulation.NumFaces > 150);
    }

    [Fact]
    public void Demo_SpatialSortingComparison()
    {
        // Arrange - Create two identical triangulations
        var triangulationSorted = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        var triangulationUnsorted = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        
        // Create points in a deliberately unsorted order
        var points = new Point2<double>[]
        {
            new(3.0, 3.0),
            new(0.0, 0.0),
            new(2.0, 1.0),
            new(1.0, 2.0),
            new(0.5, 0.5)
        };

        ReadOnlySpan<Point2<double>> span = points;

        // Act - Insert with and without spatial sorting
        triangulationSorted.InsertBulk(span, useSpatialSort: true);
        triangulationUnsorted.InsertBulk(span, useSpatialSort: false);

        // Assert - Both should produce valid triangulations with same vertex count
        Assert.Equal(5, triangulationSorted.NumVertices);
        Assert.Equal(5, triangulationUnsorted.NumVertices);
        
        // Both should produce valid Delaunay triangulations (same topology)
        // The exact face/edge counts should be identical for the same point set
        Assert.Equal(triangulationSorted.NumFaces, triangulationUnsorted.NumFaces);
        Assert.Equal(triangulationSorted.NumUndirectedEdges, triangulationUnsorted.NumUndirectedEdges);
    }

    [Fact]
    public void Demo_EmptySpanHandling()
    {
        // Arrange
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        ReadOnlySpan<Point2<double>> emptySpan = ReadOnlySpan<Point2<double>>.Empty;

        // Act - Should handle empty spans gracefully
        triangulation.InsertBulk(emptySpan);

        // Assert - Triangulation should remain empty
        Assert.Equal(0, triangulation.NumVertices);
        Assert.Equal(1, triangulation.NumFaces); // Only outer face
        Assert.Equal(0, triangulation.NumUndirectedEdges);
    }
}