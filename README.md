# Spade.NET

[![CI](https://github.com/GiantCroissant-Lunar/spade-port/actions/workflows/ci.yml/badge.svg)](https://github.com/GiantCroissant-Lunar/spade-port/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)

A .NET port of the [Rust Spade library](https://github.com/Stoeoef/spade) for high-quality Delaunay triangulation and Voronoi diagrams.

## Features

### Core (`Spade`)
- **Delaunay Triangulation** - Incremental point insertion with robust geometric predicates
- **Bulk Insertion API** - High-performance `Span<T>`-based API for inserting large point sets with zero allocations
- **Constrained Delaunay Triangulation (CDT)** - Support for constraint edges with automatic splitting
- **Voronoi Diagrams** - Dual graph extraction from Delaunay triangulation
- **Mesh Refinement** - Ruppert's algorithm with angle and area constraints
- **Natural Neighbor Interpolation** - Smooth interpolation with optional gradient support
- **Barycentric Interpolation** - Fast triangle-based interpolation
- **Spatial Queries** - Find edges/vertices in rectangles or circles
- **Robust Geometric Predicates** - Adaptive precision arithmetic (Orient2D, InCircle)
- **Line Intersection Iterator** - Efficient traversal of edges intersecting a line segment

### Advanced (`Spade.Advanced`)
- **Power Diagrams** - Weighted Voronoi diagrams
- **Clipped Voronoi** - Voronoi cells clipped to a bounding polygon with deterministic ordering and diagnostics
- **Centroidal Voronoi Relaxation** - Lloyd's algorithm for mesh smoothing

## Port Status

The port is **~95% functionally complete** compared to the Rust original:

| Feature | Status |
|---------|--------|
| Delaunay Triangulation | ✅ Complete |
| Bulk Insertion API | ✅ Complete (Span-based, optimized for large point sets) |
| Constrained Delaunay (CDT) | ✅ Complete |
| Voronoi Diagrams | ✅ Complete |
| Clipped Voronoi (Deterministic) | ✅ Complete (with ordering and diagnostics) |
| Mesh Refinement | ✅ Complete |
| Natural Neighbor Interpolation | ✅ Complete |
| Spatial Queries | ✅ Functional (simplified algorithm) |
| Robust Predicates | ✅ Complete |
| Power Diagrams | ✅ Complete |
| HierarchyHintGenerator | ⚠️ Not ported (performance feature) |
| Circle-sweep Bulk Load | ⚠️ Not ported (Rust-specific algorithm) |

**Note:** While Circle-sweep bulk load is not ported, we provide a high-performance bulk insertion API using `Span<T>` with spatial sorting and zero allocations, optimized for .NET.

## Quick Start

```csharp
using Spade;
using Spade.Primitives;

// Create a Delaunay triangulation
var triangulation = new DelaunayTriangulation<Point2<double>>();

// Insert points
triangulation.Insert(new Point2<double>(0.0, 0.0));
triangulation.Insert(new Point2<double>(1.0, 0.0));
triangulation.Insert(new Point2<double>(0.5, 1.0));

// Iterate over triangles
foreach (var face in triangulation.InnerFaces())
{
    var vertices = face.Vertices();
    Console.WriteLine($"Triangle: {vertices[0].Position}, {vertices[1].Position}, {vertices[2].Position}");
}

// Access Voronoi diagram
foreach (var voronoiFace in triangulation.VoronoiFaces())
{
    // Each Voronoi face corresponds to a Delaunay vertex
    var site = voronoiFace.AsDelaunayVertex();
}
```

### Constrained Delaunay Triangulation

```csharp
using Spade;
using Spade.Primitives;

var cdt = new ConstrainedDelaunayTriangulation<Point2<double>>();

// Insert vertices
var v0 = cdt.Insert(new Point2<double>(0.0, 0.0));
var v1 = cdt.Insert(new Point2<double>(1.0, 0.0));
var v2 = cdt.Insert(new Point2<double>(1.0, 1.0));
var v3 = cdt.Insert(new Point2<double>(0.0, 1.0));

// Add constraint edges
cdt.AddConstraint(v0, v2); // Diagonal constraint
```

### Mesh Refinement

```csharp
using Spade.Refinement;

var parameters = new RefinementParameters()
    .WithMaxAllowedArea(0.01)
    .WithAngleLimit(AngleLimit.FromDegrees(25));

var result = cdt.Refine(parameters);
Console.WriteLine($"Added {result.AddedVertices} Steiner points");
```

### Bulk Insertion (High Performance)

```csharp
using Spade;
using Spade.Primitives;

var triangulation = new DelaunayTriangulation<Point2<double>>();

// Generate or load large point set
Span<Point2<double>> points = stackalloc Point2<double>[10000];
for (int i = 0; i < points.Length; i++)
{
    points[i] = new Point2<double>(Random.Shared.NextDouble(), Random.Shared.NextDouble());
}

// Zero-allocation bulk insertion with optional spatial sorting
triangulation.InsertBulk(points, useSpatialSort: true);
```

## Project Structure

```
spade-port/
├── dotnet/
│   ├── src/
│   │   ├── Spade/              # Core library
│   │   └── Spade.Advanced/     # Power diagrams, clipped Voronoi
│   └── tests/
│       └── Spade.Tests/        # Tests, benchmarks, and performance regression tests
├── docs/
│   ├── rfcs/                   # Request for Comments documents
│   ├── specs/                  # Feature specifications
│   ├── handovers/              # Project handover documentation
│   └── reviews/                # Code review reports
├── build/                      # Build scripts (NUKE)
└── oracle-tools/               # Rust oracle for validation testing
```

## Performance

Spade.NET includes comprehensive benchmarking infrastructure to measure and track performance:

- **Bulk Insertion**: Optimized for large point sets (10k-200k+ points) with spatial sorting
- **Zero Allocations**: `Span<T>`-based APIs eliminate GC pressure during bulk operations
- **Benchmarking Suite**: BenchmarkDotNet-based performance tests with regression detection

See [BENCHMARKS.md](dotnet/tests/Spade.Tests/BENCHMARKS.md) for detailed performance characteristics and how to run benchmarks.

## Documentation

- [Usage Guide](docs/USAGE.md)
- [Migration Guide](docs/MIGRATION.md)
- [Known Differences from Rust](docs/KNOWN_DIFFERENCES.md)
- [Performance Benchmarks](dotnet/tests/Spade.Tests/BENCHMARKS.md)
- [Performance Characteristics](dotnet/tests/Spade.Tests/PERFORMANCE_CHARACTERISTICS.md)
- [RFCs](docs/rfcs/) - Design documents and implementation plans
- [Feature Specs](docs/specs/) - Detailed feature specifications

## Building

```bash
cd dotnet
dotnet build Spade.sln
dotnet test Spade.sln
```

## Requirements

- .NET 8.0 or later

## License

MIT License - see [LICENSE](LICENSE) for details.

## Acknowledgments

This is a port of the excellent [Spade](https://github.com/Stoeoef/spade) Rust library by [Stoeoef](https://github.com/Stoeoef).

## Third-Party Notices

See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for licenses of dependencies and referenced works.
