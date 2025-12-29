using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Spade;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests;

/// <summary>
/// Unit tests for edge cases in bulk insertion functionality.
/// Tests empty inputs, single points, duplicate points, API compatibility, and preallocation behavior.
/// **Requirements: 4.3, 1.2, 1.3**
/// </summary>
[Trait("Category", "UnitTests")]
public class BulkInsertionEdgeCaseTests
{
    [Fact]
    public void InsertBulk_EmptySpan_DoesNotThrow()
    {
        // Arrange
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        var emptyPoints = Array.Empty<Point2<double>>();
        var emptySpan = new ReadOnlySpan<Point2<double>>(emptyPoints);

        // Act & Assert - Don't use lambda with span
        triangulation.InsertBulk(emptySpan);
        
        // Triangulation should remain empty
        triangulation.NumVertices.Should().Be(0);
    }

    [Fact]
    public void InsertBulk_EmptyCollection_DoesNotThrow()
    {
        // Arrange
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        var emptyList = new List<Point2<double>>();

        // Act & Assert
        var action = () => triangulation.InsertBulk(emptyList);
        action.Should().NotThrow();
        
        // Triangulation should remain empty
        triangulation.NumVertices.Should().Be(0);
    }

    [Fact]
    public void InsertBulk_SinglePoint_InsertsCorrectly()
    {
        // Arrange
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        var singlePoint = new[] { new Point2<double>(1.0, 2.0) };

        // Act
        triangulation.InsertBulk(singlePoint.AsSpan());

        // Assert
        triangulation.NumVertices.Should().Be(1);
        var vertex = triangulation.Vertices().Single();
        var position = ((IHasPosition<double>)vertex.Data).Position;
        position.X.Should().BeApproximately(1.0, 1e-12);
        position.Y.Should().BeApproximately(2.0, 1e-12);
    }

    [Fact]
    public void InsertBulk_DuplicatePoints_HandlesConsistentlyWithIndividualInsert()
    {
        // Arrange
        var points = new[]
        {
            new Point2<double>(0.0, 0.0),
            new Point2<double>(1.0, 0.0),
            new Point2<double>(0.0, 1.0),
            new Point2<double>(0.0, 0.0), // Duplicate
            new Point2<double>(1.0, 0.0)  // Duplicate
        };

        var bulkTriangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        var individualTriangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

        // Act
        bulkTriangulation.InsertBulk(points.AsSpan());
        
        foreach (var point in points)
        {
            individualTriangulation.Insert(point);
        }

        // Assert - Both should handle duplicates the same way
        bulkTriangulation.NumVertices.Should().Be(individualTriangulation.NumVertices);
        bulkTriangulation.NumFaces.Should().Be(individualTriangulation.NumFaces);
        bulkTriangulation.NumUndirectedEdges.Should().Be(individualTriangulation.NumUndirectedEdges);
    }

    [Fact]
    public void InsertBulk_WithSpatialSortEnabled_ProducesValidTriangulation()
    {
        // Arrange
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        var points = new[]
        {
            new Point2<double>(3.0, 1.0),
            new Point2<double>(1.0, 3.0),
            new Point2<double>(0.0, 0.0),
            new Point2<double>(2.0, 2.0),
            new Point2<double>(4.0, 0.0)
        };

        // Act
        triangulation.InsertBulk(points.AsSpan(), useSpatialSort: true);

        // Assert
        triangulation.NumVertices.Should().Be(5);
        triangulation.NumFaces.Should().BeGreaterThan(0);
        
        // Verify all points are present
        var insertedPositions = triangulation.Vertices()
            .Select(v => ((IHasPosition<double>)v.Data).Position)
            .ToHashSet();
        
        foreach (var point in points)
        {
            insertedPositions.Should().Contain(p => 
                Math.Abs(p.X - point.X) < 1e-12 && Math.Abs(p.Y - point.Y) < 1e-12);
        }
    }

    [Fact]
    public void InsertBulk_WithSpatialSortDisabled_ProducesValidTriangulation()
    {
        // Arrange
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        var points = new[]
        {
            new Point2<double>(3.0, 1.0),
            new Point2<double>(1.0, 3.0),
            new Point2<double>(0.0, 0.0),
            new Point2<double>(2.0, 2.0),
            new Point2<double>(4.0, 0.0)
        };

        // Act
        triangulation.InsertBulk(points.AsSpan(), useSpatialSort: false);

        // Assert
        triangulation.NumVertices.Should().Be(5);
        triangulation.NumFaces.Should().BeGreaterThan(0);
        
        // Verify all points are present
        var insertedPositions = triangulation.Vertices()
            .Select(v => ((IHasPosition<double>)v.Data).Position)
            .ToHashSet();
        
        foreach (var point in points)
        {
            insertedPositions.Should().Contain(p => 
                Math.Abs(p.X - point.X) < 1e-12 && Math.Abs(p.Y - point.Y) < 1e-12);
        }
    }

    [Fact]
    public void InsertBulk_CustomVertexType_WorksCorrectly()
    {
        // Arrange
        var triangulation = new DelaunayTriangulation<TestVertex, int, int, int, LastUsedVertexHintGenerator<double>>();
        var vertices = new[]
        {
            new TestVertex { Position = new Point2<double>(0.0, 0.0), Id = 1 },
            new TestVertex { Position = new Point2<double>(1.0, 0.0), Id = 2 },
            new TestVertex { Position = new Point2<double>(0.0, 1.0), Id = 3 }
        };

        // Act
        triangulation.InsertBulk(vertices.AsSpan());

        // Assert
        triangulation.NumVertices.Should().Be(3);
        
        var insertedVertices = triangulation.Vertices().ToList();
        insertedVertices.Should().HaveCount(3);
        
        // Verify custom data is preserved
        var ids = insertedVertices.Select(v => v.Data.Id).ToHashSet();
        ids.Should().Contain(new[] { 1, 2, 3 });
    }

    [Fact]
    public void InsertBulk_NullTriangulation_ThrowsArgumentNullException()
    {
        // Arrange
        DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>? triangulation = null;
        var points = new[] { new Point2<double>(0.0, 0.0) };

        // Act & Assert
        var action = () => triangulation!.InsertBulk(points.AsSpan());
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void InsertBulk_NullCollection_ThrowsArgumentNullException()
    {
        // Arrange
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        IEnumerable<Point2<double>>? points = null;

        // Act & Assert
        var action = () => triangulation.InsertBulk(points!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void InsertBulk_VerifyPreallocationBehavior_WorksWithLargePointSet()
    {
        // Arrange
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        var points = new List<Point2<double>>();
        
        // Generate a larger set of points to test preallocation behavior
        var rng = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            points.Add(new Point2<double>(rng.NextDouble() * 100, rng.NextDouble() * 100));
        }

        // Act - Use bulk insertion which should trigger preallocation
        IEnumerable<Point2<double>> pointsEnumerable = points;
        triangulation.InsertBulk(pointsEnumerable);

        // Assert - Verify that the triangulation was built successfully
        // This indirectly tests that preallocation worked (no exceptions thrown)
        triangulation.NumVertices.Should().Be(100);
        triangulation.NumFaces.Should().BeGreaterThan(0);
        triangulation.NumUndirectedEdges.Should().BeGreaterThan(0);
        
        // Verify triangulation is valid by checking basic topology
        triangulation.NumDirectedEdges.Should().Be(triangulation.NumUndirectedEdges * 2);
    }

    [Fact]
    public void InsertBulk_APICompatibility_AllOverloadsWork()
    {
        // Test that all three overloads are available and work
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        var points = new[]
        {
            new Point2<double>(0.0, 0.0),
            new Point2<double>(1.0, 0.0),
            new Point2<double>(0.0, 1.0)
        };

        // Test ReadOnlySpan<Point2<double>> overload
        var action1 = () => triangulation.InsertBulk(points.AsSpan());
        action1.Should().NotThrow();

        // Reset triangulation
        triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

        // Test IEnumerable<Point2<double>> overload (use explicit cast to avoid ambiguity)
        IEnumerable<Point2<double>> pointsEnumerable = points;
        var action2 = () => triangulation.InsertBulk(pointsEnumerable);
        action2.Should().NotThrow();

        // Reset triangulation for custom vertex test
        var customTriangulation = new DelaunayTriangulation<TestVertex, int, int, int, LastUsedVertexHintGenerator<double>>();
        var customVertices = points.Select((p, i) => new TestVertex { Position = p, Id = i }).ToArray();

        // Test ReadOnlySpan<V> overload for custom vertex types
        var action3 = () => customTriangulation.InsertBulk(customVertices.AsSpan());
        action3.Should().NotThrow();
    }

    /// <summary>
    /// Test vertex type for custom vertex testing.
    /// </summary>
    private class TestVertex : IHasPosition<double>
    {
        public Point2<double> Position { get; set; }
        public int Id { get; set; }
    }
}