using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Spade.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Spade.Tests;

/// <summary>
/// Performance benchmarks for bulk insertion operations in Spade triangulation.
/// Measures throughput and memory allocation for various point set sizes.
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class TriangulationBenchmarks
{
    private Point2<double>[] _points1K = null!;
    private Point2<double>[] _points10K = null!;
    private Point2<double>[] _points50K = null!;
    private Point2<double>[] _points100K = null!;
    private Point2<double>[] _points200K = null!;

    private readonly Random _random = new(42); // Fixed seed for reproducible benchmarks

    [GlobalSetup]
    public void Setup()
    {
        _points1K = GenerateRandomPoints(1_000);
        _points10K = GenerateRandomPoints(10_000);
        _points50K = GenerateRandomPoints(50_000);
        _points100K = GenerateRandomPoints(100_000);
        _points200K = GenerateRandomPoints(200_000);
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

    // Bulk insertion throughput benchmarks

    [Benchmark]
    public void BulkInsert_1K_Points()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        triangulation.InsertBulk(_points1K.AsSpan(), useSpatialSort: true);
    }

    [Benchmark]
    public void BulkInsert_10K_Points()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        triangulation.InsertBulk(_points10K.AsSpan(), useSpatialSort: true);
    }

    [Benchmark]
    public void BulkInsert_50K_Points()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        triangulation.InsertBulk(_points50K.AsSpan(), useSpatialSort: true);
    }

    [Benchmark]
    public void BulkInsert_100K_Points()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        triangulation.InsertBulk(_points100K.AsSpan(), useSpatialSort: true);
    }

    [Benchmark]
    public void BulkInsert_200K_Points()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        triangulation.InsertBulk(_points200K.AsSpan(), useSpatialSort: true);
    }

    // Individual insertion comparison benchmarks (for smaller datasets)

    [Benchmark]
    public void IndividualInsert_1K_Points()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        foreach (var point in _points1K)
        {
            triangulation.Insert(point);
        }
    }

    [Benchmark]
    public void IndividualInsert_10K_Points()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        foreach (var point in _points10K)
        {
            triangulation.Insert(point);
        }
    }

    // Bulk insertion without spatial sorting benchmarks

    [Benchmark]
    public void BulkInsertNoSort_1K_Points()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        triangulation.InsertBulk(_points1K.AsSpan(), useSpatialSort: false);
    }

    [Benchmark]
    public void BulkInsertNoSort_10K_Points()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        triangulation.InsertBulk(_points10K.AsSpan(), useSpatialSort: false);
    }

    [Benchmark]
    public void BulkInsertNoSort_50K_Points()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        triangulation.InsertBulk(_points50K.AsSpan(), useSpatialSort: false);
    }

    // Memory allocation comparison benchmarks
    // These focus specifically on memory usage patterns

    [Benchmark]
    public void MemoryComparison_BulkVsIndividual_10K()
    {
        // Bulk insertion
        var triangulation1 = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        triangulation1.InsertBulk(_points10K.AsSpan(), useSpatialSort: true);
        
        // Force GC to measure actual allocation difference
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    [Benchmark]
    public void MemoryComparison_Individual_10K()
    {
        // Individual insertion for comparison
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        foreach (var point in _points10K)
        {
            triangulation.Insert(point);
        }
        
        // Force GC to measure actual allocation difference
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    [Benchmark]
    public void MemoryComparison_BulkWithPreallocation_50K()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        // Test preallocation effectiveness
        triangulation.InsertBulk(_points50K.AsSpan(), useSpatialSort: true);
    }

    /// <summary>
    /// Benchmark configuration for triangulation performance tests.
    /// Uses in-process execution to avoid overhead and enable memory diagnostics.
    /// Tracks GC collections and allocated bytes for memory analysis.
    /// </summary>
    public class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            AddJob(Job.Default
                .WithToolchain(InProcessEmitToolchain.Instance)
                .WithWarmupCount(3)
                .WithIterationCount(10)
                .WithGcServer(true)  // Use server GC for more realistic memory measurements
                .WithGcConcurrent(true));
                
            // Add memory diagnoser to track allocations and GC pressure
            AddDiagnoser(MemoryDiagnoser.Default);
            
            // Add columns to show statistics
            AddColumn(StatisticColumn.Mean);
            AddColumn(StatisticColumn.Error);
            AddColumn(StatisticColumn.StdDev);
        }
    }
}