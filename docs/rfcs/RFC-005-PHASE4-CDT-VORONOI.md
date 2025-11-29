# RFC-005: Phase 4 - Constrained Delaunay & Voronoi

**Status:** Draft
**Phase:** 4 of 5
**Duration:** Week 4 (5-7 days)
**Complexity:** ⭐⭐ Medium
**Dependencies:** Phase 2 (RFC-003), Phase 3 (RFC-004)

---

## Goal

Implement Constrained Delaunay Triangulation (CDT) and Voronoi diagram extraction.

---

## Part A: Constrained Delaunay (CDT)

### Source Files (Rust)
- `ref-projects/spade/src/cdt.rs`

### Target Files (C#)
```
CDT/
├── ConstrainedDelaunayTriangulation.cs  # Main CDT class
├── ConstraintInsertion.cs               # Add constraint edges
├── CdtEdge.cs                           # Edge with constraint flag
└── ConstraintOptions.cs                 # CDT options
```

### Key Feature: Constraint Edge Insertion

**Algorithm:**
```
ADD_CONSTRAINT(edge_ab):
  IF EDGE_EXISTS(a, b):
    MARK_AS_CONSTRAINT(edge_ab)
    RETURN

  // Find intersecting edges
  intersecting = FIND_INTERSECTING_EDGES(a, b)

  FOR EACH edge IN intersecting:
    // Remove intersecting edges
    REMOVE_EDGE(edge)

  // Retriangulate while forcing constraint
  RETRIANGULATE_WITH_CONSTRAINT(a, b)
  MARK_AS_CONSTRAINT(edge_ab)
```

### C# API
```csharp
public class ConstrainedDelaunayTriangulation<T> : DelaunayTriangulation<T>
{
    public void AddConstraint(VertexHandle v1, VertexHandle v2)
    {
        // Force edge (v1, v2) to exist in triangulation
    }

    public bool IsConstraintEdge(UndirectedEdgeHandle edge)
    {
        return constraintEdges.Contains(edge);
    }
}
```

---

## Part B: Voronoi Diagram

### Source Files (Rust)
- `ref-projects/spade/src/delaunay_triangulation.rs` (Voronoi methods)

### Target Files (C#)
```
Voronoi/
├── VoronoiVertex.cs              # Vertex (Inner/Outer)
├── VoronoiEdge.cs                # Edge (finite/infinite)
├── VoronoiFace.cs                # Face (cell)
└── VoronoiExtraction.cs          # Extraction logic
```

### Key Concept: Duality

**Delaunay ↔ Voronoi:**
- Delaunay triangle → Voronoi vertex (at circumcenter)
- Delaunay edge → Voronoi edge (perpendicular bisector)
- Delaunay vertex → Voronoi face (cell)

### C# API
```csharp
public abstract class VoronoiVertex
{
    public static VoronoiVertex Inner(Point2<double> position);
    public static VoronoiVertex Outer(DirectedEdgeHandle edge);
}

public class InnerVoronoiVertex : VoronoiVertex
{
    public Point2<double> Position { get; }
    public Point2<double> Circumcenter => Position;
}

public class OuterVoronoiVertex : VoronoiVertex
{
    public DirectedEdgeHandle Edge { get; }
}

public class VoronoiEdge
{
    public VoronoiVertex From { get; }
    public VoronoiVertex To { get; }
    public bool IsInfinite => To is OuterVoronoiVertex;
}

// Extension methods on DelaunayTriangulation
public IEnumerable<VoronoiFace> VoronoiFaces()
{
    foreach (var vertex in Vertices())
    {
        yield return new VoronoiFace(vertex, this);
    }
}

public IEnumerable<VoronoiEdge> UndirectedVoronoiEdges()
{
    foreach (var edge in UndirectedEdges())
    {
        yield return ComputeVoronoiEdge(edge);
    }
}
```

---

## Implementation Checklist

**Week 4 Day 1-3: Constrained Delaunay**
- [x] `ConstrainedDelaunayTriangulation<T>` class
- [x] Constraint edge insertion
- [x] Intersection detection
- [x] Cavity retriangulation
- [ ] Refinement integration

**Week 4 Day 4-6: Voronoi Extraction**
- [x] `VoronoiVertex` (Inner/Outer)
- [x] `VoronoiEdge` (finite/infinite)
- [x] `VoronoiFace` (cells)
- [x] Circumcenter calculation
- [x] Infinite ray handling

**Week 4 Day 7: Testing**
- [x] CDT tests (15+ tests)
- [x] Voronoi tests (15+ tests)
- [ ] Integration tests

---

## Key Tests

```csharp
[Fact]
public void CDT_PreservesConstraintEdges()
{
    var cdt = new ConstrainedDelaunayTriangulation<Point2<double>>();
    var v1 = cdt.Insert(new Point2<double>(0, 0));
    var v2 = cdt.Insert(new Point2<double>(1, 0));

    cdt.AddConstraint(v1, v2);

    // Edge should exist
    var edge = cdt.GetEdgeBetween(v1, v2);
    Assert.NotNull(edge);
    Assert.True(cdt.IsConstraintEdge(edge.Value));
}

[Fact]
public void Voronoi_VerticesAreCircumcenters()
{
    var tri = new DelaunayTriangulation<Point2<double>>();
    // ... insert points ...

    foreach (var face in tri.InnerFaces())
    {
        var (v0, v1, v2) = GetFaceVertices(face);
        var expectedCircumcenter = ComputeCircumcenter(v0, v1, v2);

        var voronoiVertex = GetVoronoiVertex(face);
        Assert.Equal(expectedCircumcenter, voronoiVertex.Position);
    }
}
```

---

## Success Criteria

✅ Can add constraint edges
✅ Constraints preserved during refinement
✅ Voronoi extraction works
✅ Infinite rays handled correctly
✅ All tests passing (30+ tests)

---

**END OF RFC-005**
