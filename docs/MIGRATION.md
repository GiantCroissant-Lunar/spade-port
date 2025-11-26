# Migration Guide: Triangle.NET → Spade for .NET

This document outlines how to migrate existing code from [Triangle.NET](https://github.com/wo80/Triangle.NET) to Spade for .NET.

> Status: Draft. This guide will be expanded with concrete examples as the API stabilizes and more real migrations are performed.

---

## 1. Concepts Mapping

| Concept                | Triangle.NET                           | Spade for .NET                                      |
|------------------------|----------------------------------------|-----------------------------------------------------|
| Point                  | `Vertex` / `Point` structs             | `Point2<double>` or custom `IHasPosition<double>`   |
| Triangulation object   | `TriangleNet.Mesh`                     | `DelaunayTriangulation<...>`                        |
| Constrained triangulation | `TriangleNet.Mesh` with segments   | `ConstrainedDelaunayTriangulation<...>`             |
| Voronoi                | `TriangleNet.Voronoi.StandardVoronoi` | `TriangulationBase.VoronoiFaces()` + helpers        |

Spade’s API is lower-level and more explicit about geometry and topology (DCEL, handles). In return, you get more control and better composability (e.g. `Spade.Advanced` features).

---

## 2. Basic Delaunay Workflow

**Triangle.NET (typical pattern):**

```csharp
var input = new TriangleNet.Geometry.InputGeometry();
input.AddPoint(0, 0);
input.AddPoint(1, 0);
input.AddPoint(0, 1);

var mesh = new TriangleNet.Mesh();
mesh.Triangulate(input);

foreach (var tri in mesh.Triangles)
{
    // tri.GetVertex(i)
}
```

**Spade equivalent:**

```csharp
using Spade;
using Spade.Primitives;

var tri = new DelaunayTriangulation<
    Point2<double>,
    int, int, int,
    LastUsedVertexHintGenerator<double>>();

tri.Insert(new Point2<double>(0, 0));
tri.Insert(new Point2<double>(1, 0));
tri.Insert(new Point2<double>(0, 1));

foreach (var face in tri.InnerFaces())
{
    var vs = face.Vertices();
    // vs[0].Data.Position, etc.
}
```

Key differences:

- Spade is purely incremental: you call `Insert` per vertex.
- Faces are accessed via `InnerFaces()` and then `face.Vertices()`.

---

## 3. Constrained Delaunay (Segments / Polygons)

Triangle.NET represents segments and polygon boundaries in `InputGeometry`. Spade exposes constraints directly:

```csharp
var cdt = new ConstrainedDelaunayTriangulation<
    Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

var v0 = cdt.Insert(new Point2<double>(0, 0));
var v1 = cdt.Insert(new Point2<double>(1, 0));
var v2 = cdt.Insert(new Point2<double>(1, 1));
var v3 = cdt.Insert(new Point2<double>(0, 1));

cdt.AddConstraint(v0, v1);
cdt.AddConstraint(v1, v2);
cdt.AddConstraint(v2, v3);
cdt.AddConstraint(v3, v0);
```

For intersecting constraints, use `AddConstraintWithSplitting` and provide a vertex factory:

```csharp
cdt.AddConstraintWithSplitting(v0, v2, p => new Point2<double>(p.X, p.Y));
```

Future versions will include higher-level polygon helpers that more closely mirror Triangle.NET’s `InputGeometry` patterns.

---

## 4. Mesh Refinement

Triangle.NET exposes quality options (minimum angle, maximum area). Spade’s Phase 3 refinement is being implemented according to RFC-004:

- `RefinementParameters` (angle limits, max area, vertex budget).
- `MeshRefinementExtensions.Refine(...)` extension for CDT.

Once the API is stable, this section will show how to translate Triangle.NET’s `QualityOptions` to Spade `RefinementParameters`.

---

## 5. Voronoi Diagrams

Triangle.NET’s `StandardVoronoi` has a dedicated API. In Spade, Voronoi is extracted from the triangulation:

```csharp
foreach (var face in tri.VoronoiFaces())
{
    var dualVertex = face.AsDelaunayVertex();
    // face.AdjacentEdges(), Voronoi vertices (finite / infinite), etc.
}
```

`Spade.Advanced` adds helpers for:

- Clipped Voronoi diagrams.
- Power diagrams / weighted Voronoi.

---

## 6. API Differences and Caveats (To Be Expanded)

- Spade is **generic** over vertex data and numeric type; Triangle.NET is double-centric.
- Spade exposes **handles** (`FixedVertexHandle`, `DirectedEdgeHandle`, etc.) instead of integer indices.
- Spade favors **incremental** operations; there is not yet a direct `Triangulate(input)` bulk API (see RFC-003 bulk loading section).

As migration experience accumulates, this document should be updated with concrete before/after snippets from real codebases.
