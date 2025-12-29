# Design Document: Bulk Build Performance

## Overview

This design implements high-performance bulk insertion for Spade.NET's Delaunay triangulation, targeting large point sets (10k-200k+ points). The implementation focuses on reducing allocations, improving cache locality, and providing measurable performance improvements over individual `Insert` calls.

The design extends the existing `BulkInsertionExtensions` with span-based APIs, preallocation strategies, and comprehensive benchmarking infrastructure.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    BulkInsertionExtensions                      │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │ InsertBulk(ReadOnlySpan<Point2<double>>)                │   │
│  │   - Zero-allocation span processing                     │   │
│  │   - Optional in-place spatial sorting                   │   │
│  │   - Preallocation hints to DCEL                         │   │
│  └─────────────────────────────────────────────────────────┘   │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │ InsertBulk(IReadOnlyList<TVertex>)                      │   │
│  │   - Compatibility with existing code                    │   │
│  │   - Delegates to span-based implementation              │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    TriangulationBase<V,DE,UE,F,L>               │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │ PreallocateForBulkInsert(int estimatedCount)            │   │
│  │   - Reserves DCEL capacity                              │   │
│  │   - Optimizes hint generator for bulk operations        │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                         DCEL<V,DE,UE,F>                         │
│  - Vertices: List<VertexData> (preallocated)                   │
│  - DirectedEdges: List<DirectedEdgeData> (preallocated)        │
│  - UndirectedEdges: List<UndirectedEdgeData> (preallocated)    │
│  - Faces: List<FaceData> (preallocated)                        │
└─────────────────────────────────────────────────────────────────┘
```

## Components and Interfaces

### BulkInsertionExtensions (Enhanced)

```csharp
public static class BulkInsertionExtensions
{
    // NEW: High-performance span-based API
    public static void InsertBulk<V, DE, UE, F, L>(
        this TriangulationBase<V, DE, UE, F, L> triangulation,
        ReadOnlySpan<Point2<double>> points,
        bool useSpatialSort = true)
        where V : IHasPosition<double>, new();

    // NEW: Vertex-based span API for custom vertex types
    public static void InsertBulk<V, DE, UE, F, L>(
        this TriangulationBase<V, DE, UE, F, L> triangulation,
        ReadOnlySpan<V> vertices,
        bool useSpatialSort = true)
        where V : IHasPosition<double>, new();

    // ENHANCED: Existing API with preallocation
    public static void InsertBulk<V, DE, UE, F, L>(
        this TriangulationBase<V, DE, UE, F, L> triangulation,
        IEnumerable<V> vertices,
        bool useSpatialSort = true)
        where V : IHasPosition<double>, new();
}
```

### TriangulationBase (Enhanced)

```csharp
public abstract class TriangulationBase<V, DE, UE, F, L>
{
    // NEW: Preallocation for bulk operations
    protected void PreallocateForBulkInsert(int estimatedVertexCount);
    
    // NEW: Bulk-optimized hint generator setup
    protected void OptimizeHintGeneratorForBulk();
}
```

### Performance Benchmarking Infrastructure

```csharp
// NEW: Benchmark suite for measuring performance
public class TriangulationBenchmarks
{
    [Benchmark]
    public void BulkInsert_1K_Points();
    
    [Benchmark]
    public void BulkInsert_10K_Points();
    
    [Benchmark]
    public void BulkInsert_50K_Points();
    
    [Benchmark]
    public void IndividualInsert_10K_Points(); // Comparison baseline
}
```

## Data Models

### BulkInsertionOptions (New)

```csharp
public readonly struct BulkInsertionOptions
{
    public bool UseSpatialSort { get; init; } = true;
    public bool PreallocateCapacity { get; init; } = true;
    public int? EstimatedCapacity { get; init; } = null;
}
```

### Performance Metrics (New)

```csharp
public readonly record struct BulkInsertionMetrics(
    TimeSpan ElapsedTime,
    int PointCount,
    double PointsPerSecond,
    long Gen0Collections,
    long Gen1Collections,
    long AllocatedBytes);
```

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Spatial sorting correctness

*For any* set of points with spatial sorting enabled, the points are processed in ascending order by X coordinate, then by Y coordinate for equal X values.

**Validates: Requirements 2.1**

### Property 2: Insertion order preservation when sorting disabled

*For any* sequence of points with spatial sorting disabled, the points are inserted in the exact order provided in the input.

**Validates: Requirements 2.3**

### Property 3: Delaunay triangulation validity

*For any* set of points inserted via bulk API, the resulting triangulation satisfies the Delaunay property: no point lies inside the circumcircle of any triangle.

**Validates: Requirements 4.1**

### Property 4: Bulk vs individual insertion equivalence

*For any* set of points, bulk insertion and individual insertion produce triangulations with identical topology (same triangles, same adjacency relationships).

**Validates: Requirements 4.2**

## Error Handling

| Scenario | Behavior |
|----------|----------|
| Empty span/collection | No-op, triangulation unchanged |
| Single point | Handled by existing single-point logic |
| Duplicate points | Consistent with individual Insert behavior |
| Invalid coordinates (NaN, Infinity) | Throws ArgumentException |
| Null triangulation | Throws ArgumentNullException |

## Testing Strategy

### Property-Based Testing

The implementation will use **FsCheck** for property-based testing:

1. **Spatial sorting correctness**: Generate random point sets, verify sorting order.
2. **Insertion order preservation**: Generate sequences, verify order when sorting disabled.
3. **Delaunay validity**: Generate point sets, verify circumcircle property.
4. **Bulk vs individual equivalence**: Generate point sets, compare topologies.

### Performance Benchmarking

The implementation will use **BenchmarkDotNet** for performance measurement:

- **Throughput benchmarks**: 1k, 10k, 50k, 100k, 200k points
- **Memory benchmarks**: GC collections, allocated bytes
- **Comparison benchmarks**: Bulk vs individual insertion
- **Regression detection**: Performance thresholds for CI

### Unit Tests

Unit tests will cover:
- API compatibility and signatures
- Edge cases: empty inputs, single points, duplicates
- Preallocation behavior verification
- Spatial sorting correctness

### Test Configuration

- Property tests: minimum 100 iterations per property
- Benchmark iterations: minimum 10 runs per size
- Performance thresholds: 10k points/sec for 50k point sets
- Test annotation format: `// **Feature: bulk-build-performance, Property N: <description>**`

## Implementation Strategy

### Phase 1: Core API Enhancement
1. Add span-based overloads to BulkInsertionExtensions
2. Implement preallocation in TriangulationBase
3. Optimize spatial sorting for in-place operations

### Phase 2: Performance Optimization
1. Profile existing insertion bottlenecks
2. Implement hint generator optimizations
3. Add DCEL capacity management

### Phase 3: Benchmarking Infrastructure
1. Set up BenchmarkDotNet integration
2. Create comprehensive benchmark suite
3. Establish performance baselines and thresholds

### Phase 4: Validation and Testing
1. Implement property-based tests
2. Add regression tests for performance
3. Validate correctness against existing behavior

## Performance Targets

Based on RFC-018 requirements and modern hardware capabilities:

| Point Count | Target Throughput | Max Time | Max GC Gen0 |
|-------------|------------------|----------|-------------|
| 1,000 | 50,000 pts/sec | 20ms | 1 collection |
| 10,000 | 25,000 pts/sec | 400ms | 5 collections |
| 50,000 | 15,000 pts/sec | 3.3s | 20 collections |
| 100,000 | 12,000 pts/sec | 8.3s | 35 collections |
| 200,000 | 10,000 pts/sec | 20s | 60 collections |

These targets represent significant improvements over naive individual insertion while maintaining correctness guarantees.