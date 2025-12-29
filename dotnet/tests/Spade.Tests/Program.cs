using BenchmarkDotNet.Running;
using System;

namespace Spade.Tests;

/// <summary>
/// Entry point for running BenchmarkDotNet benchmarks.
/// This enables running benchmarks via 'dotnet run -c Release -- --filter "*BenchmarkName*"'
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        // If no arguments provided, run all benchmarks
        if (args.Length == 0)
        {
            Console.WriteLine("Running all Spade triangulation benchmarks...");
            BenchmarkRunner.Run<TriangulationBenchmarks>();
            return;
        }

        // Otherwise, use BenchmarkSwitcher to handle command line arguments
        var switcher = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly);
        switcher.Run(args);
    }
}