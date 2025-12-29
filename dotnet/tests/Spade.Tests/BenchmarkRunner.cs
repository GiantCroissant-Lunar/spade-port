using BenchmarkDotNet.Running;
using System;

namespace Spade.Tests;

/// <summary>
/// Simple benchmark runner for triangulation performance tests.
/// Run this to execute all benchmarks and generate performance reports.
/// </summary>
public static class TriangulationBenchmarkRunner
{
    public static void RunBenchmarks()
    {
        Console.WriteLine("Starting Spade Triangulation Benchmarks...");
        Console.WriteLine("This will measure throughput and memory allocation for bulk insertion operations.");
        Console.WriteLine();
        
        // Run all benchmarks in the TriangulationBenchmarks class
        var summary = BenchmarkRunner.Run<TriangulationBenchmarks>();
        
        Console.WriteLine();
        Console.WriteLine("Benchmark completed. Results saved to BenchmarkDotNet.Artifacts directory.");
        Console.WriteLine("Key metrics to review:");
        Console.WriteLine("- Mean execution time (lower is better)");
        Console.WriteLine("- Allocated memory (lower is better)");
        Console.WriteLine("- Gen0/Gen1/Gen2 collections (lower is better)");
        Console.WriteLine("- Throughput (points/second - higher is better)");
    }
}