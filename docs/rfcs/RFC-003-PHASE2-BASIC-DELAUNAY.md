# RFC-003: Phase 2 - Basic Delaunay Triangulation

**Status:** Draft  
**Phase:** 2 of 5  
**Duration:** Week 2 (5-7 days)  
**Complexity:** ⭐⭐⭐ Hard  
**Dependencies:** Phase 1 (RFC-002)  

---

## Goal

Implement working Delaunay triangulation with incremental insertion and bulk loading.

---

## Components to Port

### Source Files (Rust)
- `ref-projects/spade/src/delaunay_triangulation.rs`
- `ref-projects/spade/src/triangulation_ext.rs`
- `ref-projects/spade/src/dcel_operations.rs`
- `ref-projects/spade/src/bulk_load.rs`

### Target Files (C#)
```
Triangulation/
├── ITriangulation.cs              # Interface
├── DelaunayTriangulation.cs       # Main class
├── TriangulationExt.cs           # Delaunay enforcement
├── EdgeFlip.cs                    # Flip algorithm
├── VertexInsertion.cs             # Incremental insertion
└── BulkLoad.cs                    # Bulk loading
```

---

## Key Algorithms

### 1. Vertex Insertion (Incremental)
**Algorithm:** Bowyer-Watson (point location + cavity retriangulation)

**Steps:**
1. Locate triangle containing new point
2. Find all triangles whose circumcircle contains the point
3. Remove these triangles (create cavity)
4. Retriangulate cavity from new point

**Rust signature:**
```rust
pub fn insert(&mut self, vertex: Point2<S>) -> Result<VertexHandle, InsertionError>
```

**C# signature:**
```csharp
public VertexHandle Insert(Point2<S> point)
```

### 2. Edge Flipping (Delaunay Property)
**Purpose:** Restore Delaunay property after insertion

**Algorithm:**
```
FLIP_EDGE(edge):
  IF edge is constrained:
    RETURN  # Don't flip constrained edges

  IF NOT should_flip(edge):
    RETURN

  # Flip the edge
  quad = GET_QUADRILATERAL(edge)
  REMOVE_EDGE(edge)
  ADD_OPPOSITE_DIAGONAL(quad)

  # Recursively check affected edges
  FLIP_EDGE(quad.edge1)
  FLIP_EDGE(quad.edge2)
```

### 3. Bulk Loading
**Purpose:** Fast initialization from point set

**Algorithm:**
1. Sort points spatially (improve cache locality)
2. Create initial triangulation (super-triangle or divide-and-conquer)
3. Incrementally add remaining points
4. Remove super-triangle

---

## Implementation Checklist

**Week 2 Day 1-2: Core Triangulation**
- [ ] `ITriangulation` interface
- [ ] `DelaunayTriangulation<T>` class skeleton
- [ ] Point location algorithm
- [ ] Basic circumcircle test

**Week 2 Day 3-4: Insertion & Flipping**
- [ ] Vertex insertion algorithm
- [ ] Cavity finding
- [ ] Cavity retriangulation
- [ ] Edge flipping algorithm
- [ ] Legalization (recursive flipping)

**Week 2 Day 5-6: Bulk Loading**
- [ ] Spatial sorting
- [ ] Bulk initialization
- [ ] Super-triangle handling

**Week 2 Day 7: Testing**
- [ ] Unit tests (20+ tests)
- [ ] Property tests (Delaunay property)
- [ ] Performance tests

---

## Key Tests

```csharp
[Fact]
public void Triangulation_SatisfiesDelaunayProperty()
{
    var tri = new DelaunayTriangulation<Point2<double>>();

    // Insert random points
    var points = GenerateRandomPoints(100);
    foreach (var p in points)
        tri.Insert(p);

    // Verify Delaunay property: no point inside any circumcircle
    foreach (var face in tri.InnerFaces())
    {
        var (v0, v1, v2) = GetFaceVertices(face);
        var circumcircle = Circumcircle(v0, v1, v2);

        foreach (var point in points)
        {
            if (point != v0 && point != v1 && point != v2)
            {
                Assert.False(circumcircle.Contains(point),
                    "Delaunay property violated");
            }
        }
    }
}
```

---

## Success Criteria

✅ Can triangulate point sets  
✅ All triangles satisfy Delaunay property  
✅ Bulk loading works  
✅ Performance: 10,000 points in <1 second  
✅ All tests passing (25+ tests)

---

**END OF RFC-003**
