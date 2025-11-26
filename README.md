# Spade.NET

A .NET port of the [Rust Spade library](https://github.com/Stoeoef/spade) for high-quality Delaunay triangulation and Voronoi diagrams.

## Features

- **Delaunay Triangulation** - Incremental point insertion with O(n log n) expected time
- **Constrained Delaunay Triangulation (CDT)** - Support for constraint edges with automatic splitting
- **Voronoi Diagrams** - Dual graph of Delaunay triangulation
- **Robust Geometric Predicates** - Adaptive precision arithmetic for numerical robustness
- **Line Intersection Iterator** - Efficient traversal of edges intersecting a line segment

## Installation

```bash
dotnet add package Spade
```

## Quick Start

```csharp
using Spade;
using Spade.Primitives;

// Create a Delaunay triangulation
var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

// Insert points
triangulation.Insert(new Point2<double>(0.0, 0.0));
triangulation.Insert(new Point2<double>(1.0, 0.0));
triangulation.Insert(new Point2<double>(0.5, 1.0));

// Iterate over triangles
foreach (var face in triangulation.InnerFaces())
{
    var edge = face.AdjacentEdge();
    // Process triangle...
}
```

## Project Structure

```
spade-port/
├── dotnet/
│   ├── src/
│   │   ├── Spade/              # Core library
│   │   └── Spade.Advanced/     # Advanced features (power diagrams, etc.)
│   └── tests/
│       └── Spade.Tests/        # Unit tests
├── docs/                       # Documentation and RFCs
├── build/                      # Build scripts (NUKE)
└── oracle-tools/               # Rust oracle for testing
```

## Documentation

- [Usage Guide](docs/USAGE.md)
- [Migration Guide](docs/MIGRATION.md)
- [Known Differences from Rust](docs/KNOWN_DIFFERENCES.md)
- [RFCs](docs/rfcs/)

## Building

```bash
cd dotnet
dotnet build Spade.sln
dotnet test Spade.sln
```

## License

MIT License - see [LICENSE](LICENSE) for details.

## Acknowledgments

This is a port of the excellent [Spade](https://github.com/Stoeoef/spade) Rust library by Stoeoef.
