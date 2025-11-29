# Spade for .NET

A robust Delaunay Triangulation library for .NET, ported from the Rust `spade` crate.

## Features

-   **Delaunay Triangulation**: Fast and robust 2D triangulation.
-   **Constrained Delaunay Triangulation (CDT)**: Supports constraint edges that must be present in the triangulation.
-   **Voronoi Diagram**: Extract Voronoi cells, edges, and vertices (including infinite rays).
-   **Robust Arithmetic**: Handles degenerate cases (collinear points, cocircular points) gracefully.
-   **Generic Design**: Customizable vertex, edge, and face data.

## Installation

(Coming soon to NuGet)

## Basic Usage

### Delaunay Triangulation

```csharp
using Spade;
using Spade.Primitives;

// Create a triangulation
var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

// Insert points
triangulation.Insert(new Point2<double>(0, 0));
triangulation.Insert(new Point2<double>(1, 0));
triangulation.Insert(new Point2<double>(0, 1));

// Iterate over faces (triangles)
foreach (var face in triangulation.InnerFaces())
{
    var vertices = face.Vertices(); // Returns 3 vertices
    Console.WriteLine($"Triangle: {vertices[0].Data.Position}, {vertices[1].Data.Position}, {vertices[2].Data.Position}");
}
```

### Constrained Delaunay Triangulation

```csharp
using Spade;
using Spade.Primitives;

var cdt = new ConstrainedDelaunayTriangulation<Point2<double>, int, CdtEdge<int>, int, LastUsedVertexHintGenerator<double>>();

var v0 = cdt.Insert(new Point2<double>(0, 0));
var v1 = cdt.Insert(new Point2<double>(2, 0));
var v2 = cdt.Insert(new Point2<double>(1, 1));
var v3 = cdt.Insert(new Point2<double>(1, -1));

// Add a constraint between v2 and v3
cdt.AddConstraint(v2, v3);

// Check if an edge is a constraint
var edge = cdt.GetEdgeFromNeighbors(v2, v3);
if (edge != null && cdt.IsConstraint(edge.Value.AsUndirected()))
{
    Console.WriteLine("Constraint exists!");
}
```

### Voronoi Diagram

```csharp
// Iterate over Voronoi faces (cells)
foreach (var face in triangulation.VoronoiFaces())
{
    // Each Voronoi face corresponds to a Delaunay vertex
    var dualVertex = face.AsDelaunayVertex();
    Console.WriteLine($"Cell for vertex at {dualVertex.Data.Position}");

    // Iterate over Voronoi edges of this cell
    foreach (var edge in face.AdjacentEdges())
    {
        var from = edge.From();
        var to = edge.To();

        if (from is VoronoiVertex<...>.Inner innerFrom && to is VoronoiVertex<...>.Inner innerTo)
        {
             Console.WriteLine($"Finite edge from {innerFrom.Position} to {innerTo.Position}");
        }
        else
        {
             Console.WriteLine("Infinite edge");
        }
    }
}
```

## Advanced Usage

### Custom Data

You can attach custom data to vertices, edges, and faces by specifying generic parameters.

```csharp
public struct MyVertex : IHasPosition<double>
{
    public Point2<double> Position { get; set; }
    public string Name { get; set; }
}

var tri = new DelaunayTriangulation<MyVertex, ...>();
```

### Clipped Voronoi Diagram (Spade.Advanced)

The `Spade.Advanced` package provides higher-level operations built on top of the core Spade triangulation, such as **clipped Voronoi diagrams**.

```csharp
using Spade;
using Spade.Primitives;
using Spade.Advanced.Voronoi;

// Create a basic triangulation
var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
triangulation.Insert(new Point2<double>(0, 0));
triangulation.Insert(new Point2<double>(2, 0));
triangulation.Insert(new Point2<double>(0, 2));
triangulation.Insert(new Point2<double>(2, 2));

// Define a convex clipping domain (e.g., map bounds)
var clipDomain = new ClipPolygon(new[]
{
    new Point2<double>(-10, -10),
    new Point2<double>(10, -10),
    new Point2<double>(10, 10),
    new Point2<double>(-10, 10),
});

// Build a clipped Voronoi diagram from the triangulation
var clipped = ClippedVoronoiBuilder.ClipToPolygon(triangulation, clipDomain);

foreach (var cell in clipped.Cells)
{
    // cell.Generator is the original vertex data (here: Point2<double>)
    // cell.Polygon is the clipped Voronoi cell polygon
    Console.WriteLine($"Generator: {cell.Generator}, vertices: {cell.Polygon.Count}");
}
```
