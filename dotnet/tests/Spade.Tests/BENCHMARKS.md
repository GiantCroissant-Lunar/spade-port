# Spade Triangulation Performance Benchmarks

This directory contains performance benchmarks for Spade's bulk insertion functionality, measuring throughput and memory allocation patterns.

## Running Benchmarks

### Option 1: Using BenchmarkDotNet CLI (Recommended)

```bash
cd spade-port/dotnet/tests/Spade.Tests
dotnet run -c Release --project . -- --filter "*TriangulationBenchmarks*"
```

### Option 2: Running Specific Benchmarks

```bash
# Run only bulk insertion benchmarks
dotnet run -c Release --project . -- --filter "*BulkInsert*"

# Run only memory comparison benchmarks  
dotnet run -c Release --project . -- --filter "*MemoryComparison*"

# Run only individual insertion benchmarks
dotnet run -c Release --project . -- --filter "*IndividualInsert*"
```

### Option 3: Using the Benchmark Runner Helper

```csharp
// In your test code or console app
TriangulationBenchmarkRunner.RunBenchmarks();
```

## Benchmark Categories

### Throughput Benchmarks
- **BulkInsert_*K_Points**: Measures bulk insertion performance for various point set sizes
- **IndividualInsert_*K_Points**: Baseline individual insertion for comparison
- **BulkInsertNoSort_*K_Points**: Bulk insertion without spatial sorting

### Memory Allocation Benchmarks
- **MemoryComparison_***: Compares memory usage between bulk and individual insertion
- All benchmarks include memory diagnostics showing:
  - Total allocated bytes
  - GC collections per generation (Gen0, Gen1, Gen2)
  - Memory allocation rate

## Performance Targets

Based on the design document, the expected performance targets are:

| Point Count | Target Throughput | Max Time | Max GC Gen0 |
|-------------|------------------|----------|-------------|
| 1,000 | 50,000 pts/sec | 20ms | 1 collection |
| 10,000 | 25,000 pts/sec | 400ms | 5 collections |
| 50,000 | 15,000 pts/sec | 3.3s | 20 collections |
| 100,000 | 12,000 pts/sec | 8.3s | 35 collections |
| 200,000 | 10,000 pts/sec | 20s | 60 collections |

## Interpreting Results

### Key Metrics
- **Mean**: Average execution time per operation
- **Error**: Statistical error margin
- **StdDev**: Standard deviation of measurements
- **Allocated**: Total memory allocated during benchmark
- **Gen0/Gen1/Gen2**: Number of garbage collections per generation

### What to Look For
1. **Bulk vs Individual**: Bulk insertion should be significantly faster
2. **Memory Efficiency**: Bulk insertion should allocate less memory per point
3. **GC Pressure**: Fewer GC collections indicate better memory management
4. **Scaling**: Performance should scale reasonably with point count

## Output Location

Benchmark results are saved to:
- `BenchmarkDotNet.Artifacts/results/` - Detailed results in multiple formats
- `BenchmarkDotNet.Artifacts/logs/` - Execution logs
- Console output shows summary table

## Requirements Validation

These benchmarks validate the following requirements:
- **Requirement 3.1**: Bulk insertion faster than individual insertion
- **Requirement 3.2**: Throughput targets for large point sets
- **Requirement 3.3**: Reduced memory allocation vs individual insertion
- **Requirement 5.1-5.3**: Performance measurement infrastructure

## Performance Regression Testing

### Automated Tests
The project includes automated performance regression tests in `PerformanceRegressionTests.cs`:

```bash
# Run performance regression tests (fast validation)
dotnet test --filter "PerformanceRegressionTests"
```

These tests validate:
- Bulk insertion meets throughput targets
- Bulk insertion is faster than individual insertion
- Memory allocation patterns don't regress
- Spatial sorting provides expected benefits

### CI Integration
Use the PowerShell script for automated CI/CD integration:

```powershell
# Run complete performance validation
.\run-performance-benchmarks.ps1 -Configuration Release -FailOnRegression

# Run with custom thresholds
.\run-performance-benchmarks.ps1 -Configuration Release -FailOnRegression $true
```

The script:
1. Builds the project in Release mode
2. Runs performance regression tests
3. Executes detailed benchmarks
4. Validates results against thresholds
5. Generates performance reports

### Performance Characteristics
See `PERFORMANCE_CHARACTERISTICS.md` for detailed analysis of:
- Performance targets and measured results
- Memory allocation patterns
- Scaling characteristics
- Hardware dependencies
- Optimization opportunities
- Troubleshooting guidance