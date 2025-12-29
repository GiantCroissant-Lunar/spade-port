# Requirements Document

## Introduction

This feature implements high-performance bulk insertion for Spade.NET's Delaunay triangulation, targeting large point sets (10k-200k+ points). The current `InsertBulk` extension is a simple wrapper that sorts and iterates, but doesn't optimize for allocation reduction or cache locality. This work addresses RFC-018 Section 3.1 "Delaunator-inspired bulk build path (Performance)".

The goal is to provide measurable throughput improvements and reduced GC pressure for worldgen pipelines that build large triangulations.

## Glossary

- **Bulk Insertion**: Inserting many vertices into a triangulation in a single operation, as opposed to individual `Insert` calls.
- **Span-based API**: An API that accepts `ReadOnlySpan<T>` to avoid allocations and enable stack-based processing.
- **Preallocation**: Reserving capacity in internal data structures before insertion to avoid repeated resizing.
- **Spatial Sort**: Ordering points by spatial coordinates (e.g., X then Y) to improve insertion locality.
- **Hint Generator**: A mechanism to accelerate point location by providing a starting vertex close to the target.
- **Throughput**: Points inserted per second.
- **GC Pressure**: The amount of garbage collection activity caused by allocations.
- **Delaunator**: A reference JavaScript library known for fast bulk triangulation.

## Requirements

### Requirement 1

**User Story:** As a worldgen developer, I want to build triangulations from large point arrays without excessive allocations, so that my generation pipeline runs faster and uses less memory.

#### Acceptance Criteria

1. WHEN inserting N points via bulk API THEN the TriangulationBase SHALL preallocate internal DCEL structures to minimize resizing operations.
2. WHEN inserting N points via bulk API THEN the BulkInsertionExtensions SHALL accept `ReadOnlySpan<Point2<double>>` to avoid array allocations.
3. WHEN inserting N points via bulk API THEN the BulkInsertionExtensions SHALL provide an overload accepting `IReadOnlyList<TVertex>` for compatibility with existing code.

### Requirement 2

**User Story:** As a worldgen developer, I want spatial sorting to be efficient and deterministic, so that my triangulation builds are fast and reproducible.

#### Acceptance Criteria

1. WHEN spatial sorting is enabled THEN the BulkInsertionExtensions SHALL sort points by X coordinate, then Y coordinate, using a stable sort.
2. WHEN spatial sorting is enabled THEN the BulkInsertionExtensions SHALL use an in-place sort when possible to avoid allocations.
3. WHEN spatial sorting is disabled THEN the BulkInsertionExtensions SHALL insert points in the order provided.

### Requirement 3

**User Story:** As a worldgen developer, I want measurable performance improvements for large point sets, so that I can justify migrating to the bulk API.

#### Acceptance Criteria

1. WHEN inserting 10,000 points via bulk API THEN the TriangulationBase SHALL complete in less time than 10,000 individual Insert calls.
2. WHEN inserting 50,000 points via bulk API THEN the TriangulationBase SHALL demonstrate throughput of at least 10,000 points/second on modern hardware.
3. WHEN inserting 100,000 points via bulk API THEN the TriangulationBase SHALL allocate less memory (measured by GC Gen0 collections) than individual Insert calls.

### Requirement 4

**User Story:** As a library maintainer, I want bulk insertion to maintain correctness guarantees, so that performance optimizations don't introduce bugs.

#### Acceptance Criteria

1. WHEN inserting points via bulk API THEN the TriangulationBase SHALL produce a valid Delaunay triangulation (all edges satisfy the Delaunay property).
2. WHEN inserting points via bulk API THEN the TriangulationBase SHALL produce the same topological result as individual Insert calls (same triangles, same adjacency).
3. WHEN inserting duplicate points via bulk API THEN the TriangulationBase SHALL handle them consistently with individual Insert behavior.

### Requirement 5

**User Story:** As a library consumer, I want to benchmark bulk insertion performance, so that I can measure improvements and regressions.

#### Acceptance Criteria

1. WHEN running performance benchmarks THEN the Spade.Tests project SHALL include a benchmark suite measuring throughput for N=1k, 10k, 50k, 100k, 200k points.
2. WHEN running performance benchmarks THEN the benchmark suite SHALL measure elapsed time, throughput (points/sec), and GC collections.
3. WHEN running performance benchmarks THEN the benchmark suite SHALL compare bulk API vs individual Insert calls.
