# RFC-002: Phase 1 - Core Data Structures

**Status:** Draft
**Phase:** 1 of 5
**Duration:** Week 1 (5-7 days)
**Complexity:** ⭐⭐ Medium
**Dependencies:** None
**Prerequisite RFC:** RFC-001 (Master Strategy)

---

## Goal

Port the foundational data structures from Spade (Rust) to create a solid foundation for the triangulation algorithms.

**Deliverables:**
- ✅ `Point2<S>` - Generic 2D point
- ✅ Handle system - References to mesh elements
- ✅ DCEL - Doubly Connected Edge List
- ✅ Geometric primitives and predicates

---

## Components to Port

### 1. Point2<S> and Geometric Primitives

**Source:** `ref-projects/spade/src/point_trait.rs`, `src/primitives.rs`
**Target:** `dotnet/src/Spade/Primitives/`

**Files to create:**
```
Primitives/
├── Point2.cs                 # Generic 2D point
├── ISpadeNum.cs             # Numeric trait (interface)
├── CoordinateValidation.cs  # Min/max coordinate checks
└── Vector2.cs               # 2D vector (if needed)
```

**Rust Source:**
```rust
// From spade/src/point_trait.rs
pub struct Point2<S> {
    pub x: S,
    pub y: S,
}

impl<S: SpadeNum> Point2<S> {
    pub fn new(x: S, y: S) -> Self {
        Point2 { x, y }
    }

    pub fn distance_2(&self, other: &Point2<S>) -> S {
        let dx = self.x - other.x;
        let dy = self.y - other.y;
        dx * dx + dy * dy
    }
}

pub trait SpadeNum: Copy + PartialOrd + /* ... */ { }
```

**C# Target:**
```csharp
namespace Spade.Primitives;

/// <summary>
/// A generic 2D point with coordinates of type <typeparamref name="S"/>.
/// </summary>
/// <typeparam name="S">The numeric type for coordinates.</typeparam>
/// <remarks>
/// Ported from: spade/src/point_trait.rs
/// </remarks>
public struct Point2<S> where S : struct, ISpadeNum<S>
{
    public S X { get; set; }
    public S Y { get; set; }

    public Point2(S x, S y)
    {
        X = x;
        Y = y;
    }

    /// <summary>
    /// Returns the squared distance to another point.
    /// </summary>
    public S DistanceSquared(Point2<S> other)
    {
        var dx = X.Subtract(other.X);
        var dy = Y.Subtract(other.Y);
        return dx.Multiply(dx).Add(dy.Multiply(dy));
    }
}

/// <summary>
/// Defines numeric operations required for coordinate types.
/// </summary>
/// <remarks>
/// Equivalent to Rust's SpadeNum trait.
/// </remarks>
public interface ISpadeNum<T> : IComparable<T>, IEquatable<T>
{
    T Add(T other);
    T Subtract(T other);
    T Multiply(T other);
    T Divide(T other);
    T Negate();
    bool IsFinite();
}

// Implement for double (most common case)
public readonly struct SpadeDouble : ISpadeNum<SpadeDouble>
{
    private readonly double value;
    public SpadeDouble(double value) => this.value = value;

    public SpadeDouble Add(SpadeDouble other) => new(value + other.value);
    public SpadeDouble Subtract(SpadeDouble other) => new(value - other.value);
    public SpadeDouble Multiply(SpadeDouble other) => new(value * other.value);
    public SpadeDouble Divide(SpadeDouble other) => new(value / other.value);
    public SpadeDouble Negate() => new(-value);
    public bool IsFinite() => double.IsFinite(value);

    public int CompareTo(SpadeDouble other) => value.CompareTo(other.value);
    public bool Equals(SpadeDouble other) => value.Equals(other.value);

    public static implicit operator double(SpadeDouble d) => d.value;
    public static implicit operator SpadeDouble(double d) => new(d);
}
```

**Implementation Checklist:**
- [ ] Create `Point2<S>` struct
- [ ] Create `ISpadeNum<T>` interface
- [ ] Implement `SpadeDouble` (for `double` type)
- [ ] Implement `SpadeFloat` (for `float` type, if needed)
- [ ] Add coordinate validation (MIN_VALUE, MAX_VALUE)
- [ ] Unit tests for Point2 operations
- [ ] Unit tests for numeric operations

---

### 2. Handle System

**Source:** `ref-projects/spade/src/handles.rs`
**Target:** `dotnet/src/Spade/Handles/`

**Files to create:**
```
Handles/
├── FixedHandleImpl.cs       # Index-based handle
├── VertexHandle.cs          # Handle to vertex
├── DirectedEdgeHandle.cs    # Handle to directed edge
├── UndirectedEdgeHandle.cs  # Handle to undirected edge
├── FaceHandle.cs            # Handle to face
└── InnerTag.cs              # Inner/Outer marker
```

**Rust Source:**
```rust
// From spade/src/handles.rs
pub struct FixedHandleImpl {
    index: usize,
}

pub struct FixedVertexHandle(FixedHandleImpl);
pub struct FixedDirectedEdgeHandle(FixedHandleImpl);
pub struct FixedFaceHandle(FixedHandleImpl);

// IMPORTANT: Directed edges use bit manipulation
// "Directed edge indices 26 and 27 both refer to undirected edge 13"
// Index = undirected_index * 2 + direction (0 or 1)
```

**C# Target (Simplified):**
```csharp
namespace Spade.Handles;

/// <summary>
/// A handle to a vertex in the triangulation.
/// </summary>
/// <remarks>
/// Ported from: spade/src/handles.rs:FixedVertexHandle
/// </remarks>
public readonly struct VertexHandle : IEquatable<VertexHandle>
{
    public int Index { get; }

    public VertexHandle(int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));
        Index = index;
    }

    public bool Equals(VertexHandle other) => Index == other.Index;
    public override bool Equals(object? obj) => obj is VertexHandle other && Equals(other);
    public override int GetHashCode() => Index;

    public static bool operator ==(VertexHandle left, VertexHandle right) => left.Equals(right);
    public static bool operator !=(VertexHandle left, VertexHandle right) => !left.Equals(right);
}

/// <summary>
/// A handle to a directed edge in the triangulation.
/// </summary>
/// <remarks>
/// Directed edges are stored in pairs. The opposite direction can be obtained
/// by flipping the last bit of the index.
///
/// Ported from: spade/src/handles.rs:FixedDirectedEdgeHandle
/// </remarks>
public readonly struct DirectedEdgeHandle : IEquatable<DirectedEdgeHandle>
{
    public int Index { get; }

    public DirectedEdgeHandle(int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));
        Index = index;
    }

    /// <summary>
    /// Returns the handle to the opposite direction of this edge.
    /// </summary>
    /// <remarks>
    /// PORTING NOTE: Rust uses bit manipulation (index XOR 1).
    /// In C#, we use the same approach for compatibility.
    /// </remarks>
    public DirectedEdgeHandle Opposite()
    {
        return new DirectedEdgeHandle(Index ^ 1);
    }

    /// <summary>
    /// Returns the undirected edge handle for this directed edge.
    /// </summary>
    public UndirectedEdgeHandle AsUndirected()
    {
        return new UndirectedEdgeHandle(Index >> 1);
    }

    public bool Equals(DirectedEdgeHandle other) => Index == other.Index;
    public override bool Equals(object? obj) => obj is DirectedEdgeHandle other && Equals(other);
    public override int GetHashCode() => Index;
}

/// <summary>
/// A handle to an undirected edge in the triangulation.
/// </summary>
public readonly struct UndirectedEdgeHandle : IEquatable<UndirectedEdgeHandle>
{
    public int Index { get; }

    public UndirectedEdgeHandle(int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));
        Index = index;
    }

    /// <summary>
    /// Returns one of the two directed edge handles for this undirected edge.
    /// </summary>
    public DirectedEdgeHandle AsDirected(int direction = 0)
    {
        if (direction != 0 && direction != 1)
            throw new ArgumentException("Direction must be 0 or 1", nameof(direction));
        return new DirectedEdgeHandle((Index << 1) | direction);
    }

    public bool Equals(UndirectedEdgeHandle other) => Index == other.Index;
    public override bool Equals(object? obj) => obj is UndirectedEdgeHandle other && Equals(other);
    public override int GetHashCode() => Index;
}

/// <summary>
/// A handle to a face in the triangulation.
/// </summary>
public readonly struct FaceHandle : IEquatable<FaceHandle>
{
    public int Index { get; }

    public FaceHandle(int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));
        Index = index;
    }

    public bool Equals(FaceHandle other) => Index == other.Index;
    public override bool Equals(object? obj) => obj is FaceHandle other && Equals(other);
    public override int GetHashCode() => Index;
}
```

**Implementation Checklist:**
- [ ] Create `VertexHandle` struct
- [ ] Create `DirectedEdgeHandle` struct with opposite/undirected methods
- [ ] Create `UndirectedEdgeHandle` struct with directed method
- [ ] Create `FaceHandle` struct
- [ ] Unit tests for handle creation and equality
- [ ] Unit tests for directed ↔ undirected conversion
- [ ] Unit tests for opposite edge

---

### 3. DCEL (Doubly Connected Edge List)

**Source:** `ref-projects/spade/src/dcel.rs`
**Target:** `dotnet/src/Spade/DCEL/`

**Files to create:**
```
DCEL/
├── Dcel.cs                # Main DCEL class
├── VertexEntry.cs         # Vertex data
├── DirectedEdgeEntry.cs   # Directed edge data
├── FaceEntry.cs           # Face data
└── DcelOperations.cs      # Topology operations
```

**Rust Source (simplified):**
```rust
// From spade/src/dcel.rs
pub struct Dcel<V, DE, UE, F> {
    vertices: Vec<VertexEntry<V, DE>>,
    directed_edges: Vec<DirectedEdgeEntry<DE, UE>>,
    undirected_edges: Vec<UndirectedEdgeEntry<UE>>,
    faces: Vec<FaceEntry<F, DE>>,
}

pub struct VertexEntry<V, DE> {
    pub data: V,
    pub out_edge: Option<FixedDirectedEdgeHandle>,
}

pub struct DirectedEdgeEntry<DE, UE> {
    pub data: DE,
    pub origin: FixedVertexHandle,
    pub twin: FixedDirectedEdgeHandle,
    pub next: FixedDirectedEdgeHandle,
    pub face: FixedFaceHandle,
    pub undirected_edge: FixedUndirectedEdgeHandle,
}

pub struct FaceEntry<F, DE> {
    pub data: F,
    pub adjacent_edge: Option<FixedDirectedEdgeHandle>,
}
```

**C# Target:**
```csharp
namespace Spade.DCEL;

/// <summary>
/// Doubly Connected Edge List - the core topology data structure.
/// </summary>
/// <typeparam name="V">Vertex data type</typeparam>
/// <typeparam name="DE">Directed edge data type</typeparam>
/// <typeparam name="UE">Undirected edge data type</typeparam>
/// <typeparam name="F">Face data type</typeparam>
/// <remarks>
/// Ported from: spade/src/dcel.rs
///
/// The DCEL stores the topological structure of the triangulation.
/// Each vertex, edge, and face has associated data of generic type.
/// </remarks>
public class Dcel<V, DE, UE, F>
{
    private List<VertexEntry<V>> vertices = new();
    private List<DirectedEdgeEntry<DE>> directedEdges = new();
    private List<UndirectedEdgeEntry<UE>> undirectedEdges = new();
    private List<FaceEntry<F>> faces = new();

    public int NumVertices => vertices.Count;
    public int NumDirectedEdges => directedEdges.Count;
    public int NumUndirectedEdges => undirectedEdges.Count;
    public int NumFaces => faces.Count;

    /// <summary>
    /// Adds a new isolated vertex.
    /// </summary>
    public VertexHandle AddVertex(V data)
    {
        var index = vertices.Count;
        vertices.Add(new VertexEntry<V>
        {
            Data = data,
            OutEdge = null
        });
        return new VertexHandle(index);
    }

    /// <summary>
    /// Gets vertex data for the given handle.
    /// </summary>
    public ref V GetVertex(VertexHandle handle)
    {
        return ref vertices[handle.Index].Data;
    }

    /// <summary>
    /// Gets the outgoing edge of a vertex.
    /// </summary>
    public DirectedEdgeHandle? GetOutEdge(VertexHandle handle)
    {
        return vertices[handle.Index].OutEdge;
    }

    /// <summary>
    /// Sets the outgoing edge of a vertex.
    /// </summary>
    public void SetOutEdge(VertexHandle handle, DirectedEdgeHandle? edge)
    {
        vertices[handle.Index].OutEdge = edge;
    }

    // Similar methods for edges and faces...
}

/// <summary>
/// Data stored for each vertex in the DCEL.
/// </summary>
public struct VertexEntry<V>
{
    public V Data;
    public DirectedEdgeHandle? OutEdge;
}

/// <summary>
/// Data stored for each directed edge in the DCEL.
/// </summary>
public struct DirectedEdgeEntry<DE>
{
    public DE Data;
    public VertexHandle Origin;
    public DirectedEdgeHandle Twin;
    public DirectedEdgeHandle Next;
    public FaceHandle Face;
    public UndirectedEdgeHandle UndirectedEdge;
}

/// <summary>
/// Data stored for each undirected edge in the DCEL.
/// </summary>
public struct UndirectedEdgeEntry<UE>
{
    public UE Data;
}

/// <summary>
/// Data stored for each face in the DCEL.
/// </summary>
public struct FaceEntry<F>
{
    public F Data;
    public DirectedEdgeHandle? AdjacentEdge;
}
```

**Implementation Checklist:**
- [ ] Create `Dcel<V, DE, UE, F>` class
- [ ] Create entry structs (VertexEntry, DirectedEdgeEntry, etc.)
- [ ] Implement Add/Get methods for vertices
- [ ] Implement Add/Get methods for edges
- [ ] Implement Add/Get methods for faces
- [ ] Implement navigation methods (next, twin, etc.)
- [ ] Unit tests for DCEL creation
- [ ] Unit tests for topology navigation

---

## Testing Requirements

### Unit Tests

**File:** `tests/Spade.Tests/Primitives/Point2Tests.cs`
```csharp
public class Point2Tests
{
    [Fact]
    public void Point2_Construction_SetsCoordinates()
    {
        var point = new Point2<double>(3.0, 4.0);
        Assert.Equal(3.0, point.X);
        Assert.Equal(4.0, point.Y);
    }

    [Fact]
    public void DistanceSquared_ReturnsCorrectValue()
    {
        var p1 = new Point2<double>(0.0, 0.0);
        var p2 = new Point2<double>(3.0, 4.0);

        var distSq = p1.DistanceSquared(p2);

        Assert.Equal(25.0, distSq); // 3^2 + 4^2 = 25
    }
}
```

**File:** `tests/Spade.Tests/Handles/HandleTests.cs`
```csharp
public class HandleTests
{
    [Fact]
    public void DirectedEdgeHandle_Opposite_FlipsDirection()
    {
        var edge = new DirectedEdgeHandle(26);
        var opposite = edge.Opposite();

        Assert.Equal(27, opposite.Index); // 26 XOR 1 = 27
    }

    [Fact]
    public void DirectedEdgeHandle_AsUndirected_ReturnsCorrectIndex()
    {
        var edge1 = new DirectedEdgeHandle(26);
        var edge2 = new DirectedEdgeHandle(27);

        var undirected1 = edge1.AsUndirected();
        var undirected2 = edge2.AsUndirected();

        Assert.Equal(13, undirected1.Index); // 26 >> 1 = 13
        Assert.Equal(13, undirected2.Index); // 27 >> 1 = 13
    }
}
```

**File:** `tests/Spade.Tests/DCEL/DcelTests.cs`
```csharp
public class DcelTests
{
    [Fact]
    public void Dcel_AddVertex_IncreasesCount()
    {
        var dcel = new Dcel<Point2<double>, int, int, int>();

        Assert.Equal(0, dcel.NumVertices);

        var v1 = dcel.AddVertex(new Point2<double>(0, 0));
        Assert.Equal(1, dcel.NumVertices);

        var v2 = dcel.AddVertex(new Point2<double>(1, 0));
        Assert.Equal(2, dcel.NumVertices);
    }

    [Fact]
    public void Dcel_GetVertex_ReturnsCorrectData()
    {
        var dcel = new Dcel<Point2<double>, int, int, int>();
        var point = new Point2<double>(3.0, 4.0);

        var handle = dcel.AddVertex(point);
        var retrieved = dcel.GetVertex(handle);

        Assert.Equal(point, retrieved);
    }
}
```

---

## Success Criteria

✅ **All components implemented:**
- Point2<S> with numeric operations
- Handle system (Vertex, Edge, Face handles)
- DCEL with basic operations

✅ **All tests passing:**
- Point2 tests (10+ tests)
- Handle tests (10+ tests)
- DCEL tests (15+ tests)

✅ **Code quality:**
- No compiler warnings
- XML documentation complete
- Porting notes documented

✅ **Ready for Phase 2:**
- Can create basic mesh structures
- Navigation works correctly
- Performance acceptable

---

## Estimated Timeline

| Task | Estimated Time | Priority |
|------|---------------|----------|
| Point2<S> and ISpadeNum | 1 day | High |
| Handle system | 1-2 days | High |
| DCEL data structures | 2-3 days | High |
| Unit tests | 1-2 days | High |
| Documentation & review | 0.5-1 day | Medium |
| **Total** | **5-7 days** | - |

---

## Porting Notes

### Key Decisions

1. **ISpadeNum interface:** Created to match Rust's `SpadeNum` trait
   - Allows generic numeric operations
   - Implement for `double` and `float`

2. **Handle bit manipulation:** Preserved from Rust
   - Directed edges use (undirected_index * 2 + direction)
   - Allows O(1) opposite edge lookup

3. **DCEL ref returns:** Used `ref` returns for performance
   - Avoids copying large structs
   - More Rust-like semantics

4. **Nullable handles:** Used `?` for optional handles
   - More C#-idiomatic than Option<T> wrapper

---

## Next Steps

After Phase 1 completion:
1. ✅ Review and merge Phase 1 code
2. ➡️ Begin RFC-003 (Phase 2: Basic Delaunay)
3. ➡️ Port triangulation algorithms

---

**END OF RFC-002**
