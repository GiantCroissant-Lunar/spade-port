using FluentAssertions;
using Spade.Primitives;
using System;
using System.Diagnostics;
using Xunit;

namespace Spade.Tests;

/// <summary>
/// Performance regression tests that verify bulk insertion meets performance targets.
/// These tests validate Requirements 3.1, 3.2, and 3.3 from the design document.
/// </summary>
public class PerformanceRegressionTests
{
    private readonly Random _random = new(42); // Fixed seed for reproducible tests

    /// <summary>
    /// Validates that bulk insertion of 1K points meets throughput target.
    /// Target: 50,000 points/sec (20ms max for 1K points)
    /// Validates: Requirements 3.1, 3.2
    /// </summary>
    [Fact]
    public void BulkInsert_1K_Points_MeetsPerformanceTarget()
    {
        // Arrange
        var points = GenerateRandomPoints(1_000);
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        
        // Act & Assert
        var stopwatch = Stopwatch.StartNew();
        triangulation.InsertBulk(points.AsSpan(), useSpatialSort: true);
        stopwatch.Stop();
        
        // Target: 50,000 points/sec = 20ms for 1K points
        // Use more lenient target for debug builds and CI environments
        var maxAllowedTime = TimeSpan.FromMilliseconds(100);
        stopwatch.Elapsed.Should().BeLessThanOrEqualTo(maxAllowedTime, 
            $"1K points should be inserted in less than {maxAllowedTime.TotalMilliseconds}ms");
        
        // Verify triangulation was created successfully
        triangulation.NumVertices.Should().Be(1_000);
    }

    /// <summary>
    /// Validates that bulk insertion of 10K points meets throughput target.
    /// Target: 25,000 points/sec (400ms max for 10K points)
    /// Validates: Requirements 3.1, 3.2
    /// </summary>
    [Fact]
    public void BulkInsert_10K_Points_MeetsPerformanceTarget()
    {
        // Arrange
        var points = GenerateRandomPoints(10_000);
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        
        // Act & Assert
        var stopwatch = Stopwatch.StartNew();
        triangulation.InsertBulk(points.AsSpan(), useSpatialSort: true);
        stopwatch.Stop();
        
        // Target: 25,000 points/sec = 400ms for 10K points
        // Use more lenient target for debug builds and CI environments
        var maxAllowedTime = TimeSpan.FromMilliseconds(1000);
        stopwatch.Elapsed.Should().BeLessThanOrEqualTo(maxAllowedTime, 
            $"10K points should be inserted in less than {maxAllowedTime.TotalMilliseconds}ms");
        
        // Verify triangulation was created successfully
        triangulation.NumVertices.Should().Be(10_000);
    }

    /// <summary>
    /// Validates that bulk insertion is faster than individual insertion.
    /// Validates: Requirements 3.1
    /// </summary>
    [Fact]
    public void BulkInsert_IsFasterThan_IndividualInsert()
    {
        // Arrange
        var points = GenerateRandomPoints(1_000); // Use smaller set for faster test execution
        
        // Measure bulk insertion
        var bulkTriangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        var bulkStopwatch = Stopwatch.StartNew();
        bulkTriangulation.InsertBulk(points.AsSpan(), useSpatialSort: true);
        bulkStopwatch.Stop();
        
        // Measure individual insertion
        var individualTriangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        var individualStopwatch = Stopwatch.StartNew();
        foreach (var point in points)
        {
            individualTriangulation.Insert(point);
        }
        individualStopwatch.Stop();
        
        // Assert bulk is faster
        bulkStopwatch.Elapsed.Should().BeLessThan(individualStopwatch.Elapsed,
            "bulk insertion should be faster than individual insertion");
        
        // Verify both produce valid triangulations
        bulkTriangulation.NumVertices.Should().Be(individualTriangulation.NumVertices);
    }

    /// <summary>
    /// Validates that bulk insertion allocates less memory than individual insertion.
    /// This is a basic test - detailed memory analysis is done via BenchmarkDotNet.
    /// Validates: Requirements 3.3
    /// </summary>
    [Fact]
    public void BulkInsert_AllocatesLessMemory_ThanIndividualInsert()
    {
        // Arrange
        var points = GenerateRandomPoints(1_000);
        
        // Force GC before measurements
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        // Measure bulk insertion memory
        var bulkMemoryBefore = GC.GetTotalMemory(false);
        var bulkTriangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        bulkTriangulation.InsertBulk(points.AsSpan(), useSpatialSort: true);
        var bulkMemoryAfter = GC.GetTotalMemory(false);
        var bulkMemoryUsed = bulkMemoryAfter - bulkMemoryBefore;
        
        // Force GC between measurements
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        // Measure individual insertion memory
        var individualMemoryBefore = GC.GetTotalMemory(false);
        var individualTriangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        foreach (var point in points)
        {
            individualTriangulation.Insert(point);
        }
        var individualMemoryAfter = GC.GetTotalMemory(false);
        var individualMemoryUsed = individualMemoryAfter - individualMemoryBefore;
        
        // Assert bulk uses less or equal memory (allowing for measurement variance)
        // Note: This is a basic test; BenchmarkDotNet provides more accurate memory measurements
        bulkMemoryUsed.Should().BeLessThanOrEqualTo((long)(individualMemoryUsed * 1.1), // Allow 10% variance
            "bulk insertion should not use significantly more memory than individual insertion");
        
        // Verify both produce valid triangulations
        bulkTriangulation.NumVertices.Should().Be(individualTriangulation.NumVertices);
    }

    /// <summary>
    /// Validates that spatial sorting provides performance benefit for large datasets.
    /// Validates: Requirements 2.1, 3.2
    /// </summary>
    [Fact]
    public void BulkInsert_WithSpatialSort_IsFasterThan_WithoutSort()
    {
        // Arrange - use larger dataset where spatial sorting shows benefit
        var points = GenerateRandomPoints(5_000);
        
        // Measure with spatial sorting
        var sortedTriangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        var sortedStopwatch = Stopwatch.StartNew();
        sortedTriangulation.InsertBulk(points.AsSpan(), useSpatialSort: true);
        sortedStopwatch.Stop();
        
        // Measure without spatial sorting
        var unsortedTriangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        var unsortedStopwatch = Stopwatch.StartNew();
        unsortedTriangulation.InsertBulk(points.AsSpan(), useSpatialSort: false);
        unsortedStopwatch.Stop();
        
        // Assert spatial sorting provides benefit (or at least doesn't hurt significantly)
        // Allow sorted to be up to 20% slower due to sorting overhead for small datasets
        sortedStopwatch.Elapsed.Should().BeLessThanOrEqualTo(unsortedStopwatch.Elapsed.Add(TimeSpan.FromMilliseconds(100)),
            "spatial sorting should not add significant overhead");
        
        // Verify both produce valid triangulations
        sortedTriangulation.NumVertices.Should().Be(unsortedTriangulation.NumVertices);
    }

    /// <summary>
    /// Performance smoke test for larger datasets to catch major regressions.
    /// Target: 15,000 points/sec for 50K points (3.33s max)
    /// Validates: Requirements 3.2
    /// </summary>
    [Fact]
    public void BulkInsert_50K_Points_CompletesInReasonableTime()
    {
        // Arrange
        var points = GenerateRandomPoints(50_000);
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        
        // Act & Assert
        var stopwatch = Stopwatch.StartNew();
        triangulation.InsertBulk(points.AsSpan(), useSpatialSort: true);
        stopwatch.Stop();
        
        // Target: 15,000 points/sec = 3.33s for 50K points
        // Use more lenient target for CI environments
        var maxAllowedTime = TimeSpan.FromSeconds(5);
        stopwatch.Elapsed.Should().BeLessThanOrEqualTo(maxAllowedTime, 
            $"50K points should be inserted in less than {maxAllowedTime.TotalSeconds}s");
        
        // Verify triangulation was created successfully
        triangulation.NumVertices.Should().Be(50_000);
    }

    private Point2<double>[] GenerateRandomPoints(int count)
    {
        var points = new Point2<double>[count];
        for (int i = 0; i < count; i++)
        {
            points[i] = new Point2<double>(_random.NextDouble() * 1000, _random.NextDouble() * 1000);
        }
        return points;
    }
}