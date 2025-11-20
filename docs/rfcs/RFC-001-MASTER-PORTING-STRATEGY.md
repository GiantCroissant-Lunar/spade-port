# RFC-001: Master Porting Strategy - Spade (Rust) to .NET

**Status:** Draft → Active
**Created:** 2025-01-20
**Target:** .NET 8.0 / .NET 9.0
**Source:** Spade (Rust) - https://github.com/Stoeoef/spade
**License:** MIT OR Apache-2.0 → MIT (choose MIT for simplicity)

---

## Executive Summary

This RFC defines the **master strategy** for porting Spade, a production-ready Rust library for Delaunay triangulation and Voronoi diagrams, to .NET. This port will replace Triangle.NET, resolving licensing issues while maintaining full feature parity.

**Goal:** Create a high-quality, MIT-licensed .NET library providing:
- ✅ Delaunay triangulation
- ✅ Mesh refinement (quality constraints)
- ✅ Constrained Delaunay (CDT)
- ✅ Voronoi diagrams
- ✅ Robust geometric predicates

**Timeline:** 3-4 weeks (full-time developer)

---

## Project Structure

```
spade-port/
├── ref-projects/
│   └── spade/                    # Reference Rust implementation (cloned)
├── dotnet/
│   ├── src/
│   │   └── Spade/                # Main library
│   │       ├── Primitives/       # Point2, coordinates
│   │       ├── Handles/          # Reference system
│   │       ├── DCEL/             # Doubly Connected Edge List
│   │       ├── Triangulation/    # Core Delaunay
│   │       ├── Refinement/       # Mesh refinement
│   │       ├── CDT/              # Constrained Delaunay
│   │       └── Voronoi/          # Voronoi extraction
│   └── tests/
│       └── Spade.Tests/          # Comprehensive test suite
└── docs/
    ├── rfcs/                     # This directory
    │   ├── RFC-001-*.md          # Master strategy (this file)
    │   ├── RFC-002-*.md          # Phase 1: Core structures
    │   ├── RFC-003-*.md          # Phase 2: Basic Delaunay
    │   ├── RFC-004-*.md          # Phase 3: Refinement
    │   ├── RFC-005-*.md          # Phase 4: CDT & Voronoi
    │   └── RFC-006-*.md          # Phase 5: Integration
    ├── api/                      # API documentation
    ├── porting-notes/            # Rust→C# translation notes
    └── architecture/             # Architecture decisions
```

---

## Porting Philosophy

### Core Principles

1. **Faithful Port, Not Literal Translation**
   - Preserve algorithms and semantics
   - Adapt to C# idioms and best practices
   - Don't blindly copy Rust patterns

2. **Maintain Quality**
   - Comprehensive unit tests (port from Spade)
   - Same correctness guarantees
   - Performance within 2x of Rust version

3. **Clear Documentation**
   - Document every porting decision
   - Link to original Rust code
   - Explain algorithm choices

4. **Incremental Approach**
   - Build and test each phase
   - Don't move forward until tests pass
   - Maintain working code at all times

---

## Porting Phases

### Overview

| Phase | Duration | Complexity | Dependencies | Deliverable |
|-------|----------|-----------|--------------|-------------|
| **Phase 1** | Week 1 | ⭐⭐ Medium | None | Core data structures |
| **Phase 2** | Week 2 | ⭐⭐⭐ Hard | Phase 1 | Basic Delaunay |
| **Phase 3** | Week 3 | ⭐⭐⭐ Hard | Phase 2 | Mesh refinement |
| **Phase 4** | Week 4 | ⭐⭐ Medium | Phase 2, 3 | CDT & Voronoi |
| **Phase 5** | Week 4-5 | ⭐ Easy | All | Integration |

### Phase 1: Core Data Structures (RFC-002)

**Goal:** Foundation types and data structures

**Components:**
- `Point2<S>` - 2D point with generic coordinate type
- Handle system - References to triangulation elements
- DCEL - Doubly Connected Edge List
- Geometric primitives

**Output:** `Spade.Primitives`, `Spade.Handles`, `Spade.DCEL` namespaces

**Success Criteria:**
- Can create points and handles
- DCEL can represent simple meshes
- All unit tests pass

### Phase 2: Basic Delaunay Triangulation (RFC-003)

**Goal:** Working Delaunay triangulation

**Components:**
- `DelaunayTriangulation<T>` class
- Vertex insertion (incremental)
- Edge flipping (Delaunay property)
- Bulk loading
- Convex hull

**Output:** `Spade.Triangulation` namespace

**Success Criteria:**
- Can triangulate point sets
- All triangles satisfy Delaunay property
- Bulk loading works
- Performance acceptable

### Phase 3: Mesh Refinement (RFC-004)

**Goal:** Quality-constrained mesh generation

**Components:**
- `RefinementParameters<S>`
- `AngleLimit`
- Steiner point insertion
- Refinement algorithm

**Output:** `Spade.Refinement` namespace

**Success Criteria:**
- Can enforce minimum angle constraints
- Can enforce maximum area constraints
- All triangles meet quality requirements
- Algorithm terminates

### Phase 4: Constrained Delaunay & Voronoi (RFC-005)

**Goal:** Complete feature set

**Components:**
- `ConstrainedDelaunayTriangulation<T>`
- Constraint edge insertion
- Voronoi face/edge/vertex extraction
- Infinite ray handling

**Output:** `Spade.CDT`, `Spade.Voronoi` namespaces

**Success Criteria:**
- Constraint edges preserved
- Voronoi extraction correct
- Handles infinite edges properly

### Phase 5: Integration & Testing (RFC-006)

**Goal:** Production-ready library

**Components:**
- Comprehensive test suite
- Performance benchmarks
- API documentation
- Usage examples
- NuGet packaging

**Output:** Spade NuGet package

**Success Criteria:**
- All tests pass
- Performance comparable to Triangle.NET
- Documentation complete
- Ready for use in fantasy-map-generator

---

## Rust → C# Translation Guide

### Language Feature Mapping

| Rust | C# | Notes |
|------|-----|-------|
| `struct` | `struct` or `class` | Use `struct` for small value types |
| `enum` (with data) | abstract class + inheritance | Or use discriminated unions library |
| `trait` | `interface` | Similar concept |
| `impl Trait for Type` | `class Type : ITrait` | Direct mapping |
| `&T` (immutable ref) | `T` or `in T` | C# uses references by default for classes |
| `&mut T` (mutable ref) | `ref T` | Or just `T` for classes |
| `Option<T>` | `T?` (nullable) | For reference types or `Nullable<T>` |
| `Result<T, E>` | Exceptions or `Result<T>` type | Choose consistent strategy |
| `Vec<T>` | `List<T>` | Dynamic array |
| `[T; N]` | `T[]` or `T[N]` (fixed) | Stack-allocated in Rust, heap in C# |
| Generics `<T>` | Generics `<T>` | Very similar |
| Pattern matching | `switch` expressions | C# 8+ has good pattern matching |

### Type System Differences

**Rust:**
```rust
pub struct Point2<S> {
    pub x: S,
    pub y: S,
}

impl<S: SpadeNum> Point2<S> {
    pub fn distance(&self, other: &Point2<S>) -> S {
        // ...
    }
}
```

**C#:**
```csharp
public struct Point2<S> where S : ISpadeNum
{
    public S X { get; set; }
    public S Y { get; set; }

    public S Distance(Point2<S> other)
    {
        // ...
    }
}
```

**Key Differences:**
1. Rust: Methods in `impl` blocks → C#: Methods in type definition
2. Rust: Trait bounds (`S: SpadeNum`) → C#: Generic constraints (`where S : ISpadeNum`)
3. Rust: `&self` → C#: `this` (implicit)
4. Rust: `pub` explicit → C#: `public` explicit

### Ownership & Borrowing

**Rust's ownership model doesn't exist in C#.** However, we can maintain similar patterns:

**Rust:**
```rust
fn get_vertex(&self, handle: VertexHandle) -> &Vertex {
    &self.vertices[handle.index()]
}

fn get_vertex_mut(&mut self, handle: VertexHandle) -> &mut Vertex {
    &mut self.vertices[handle.index()]
}
```

**C# (Option 1: Direct access):**
```csharp
public Vertex GetVertex(VertexHandle handle)
{
    return vertices[handle.Index];
}

public void SetVertex(VertexHandle handle, Vertex value)
{
    vertices[handle.Index] = value;
}
```

**C# (Option 2: Ref returns - more Rust-like):**
```csharp
public ref Vertex GetVertex(VertexHandle handle)
{
    return ref vertices[handle.Index];
}
```

**Decision:** Use Option 1 for simplicity, Option 2 for performance-critical code.

### Error Handling

**Rust:**
```rust
pub fn insert(&mut self, point: Point2<S>) -> Result<VertexHandle, InsertionError> {
    // ...
}
```

**C# (Option 1: Exceptions):**
```csharp
public VertexHandle Insert(Point2<S> point)
{
    if (/* error condition */)
        throw new InsertionException("...");
    return handle;
}
```

**C# (Option 2: Result type):**
```csharp
public Result<VertexHandle, InsertionError> Insert(Point2<S> point)
{
    // ...
}
```

**Decision:** Use exceptions for consistency with .NET conventions.

### Iterators

**Rust:**
```rust
pub fn vertices(&self) -> impl Iterator<Item = &Vertex> {
    self.vertices.iter()
}
```

**C#:**
```csharp
public IEnumerable<Vertex> Vertices()
{
    return vertices;
}

// Or more explicit:
public IEnumerable<Vertex> Vertices()
{
    foreach (var vertex in vertices)
        yield return vertex;
}
```

**Decision:** Return `IEnumerable<T>` for consistency with .NET.

---

## Naming Conventions

### Rust → C# Name Mapping

| Rust Convention | C# Convention | Example |
|----------------|---------------|---------|
| `snake_case` | `PascalCase` | `insert_vertex` → `InsertVertex` |
| `SCREAMING_SNAKE` | `PascalCase` | `MAX_VALUE` → `MaxValue` |
| Module | Namespace | `spade::delaunay` → `Spade.Triangulation` |
| Trait | Interface | `SpadeNum` → `ISpadeNum` |

### Namespace Structure

```csharp
namespace Spade
{
    // Root namespace - public API
}

namespace Spade.Primitives
{
    // Point2<S>, coordinates
}

namespace Spade.Handles
{
    // VertexHandle, EdgeHandle, FaceHandle
}

namespace Spade.DCEL
{
    // DoublyConnectedEdgeList, Vertex, Edge, Face
}

namespace Spade.Triangulation
{
    // DelaunayTriangulation<T>, ITriangulation
}

namespace Spade.Refinement
{
    // RefinementParameters<S>, AngleLimit
}

namespace Spade.CDT
{
    // ConstrainedDelaunayTriangulation<T>
}

namespace Spade.Voronoi
{
    // VoronoiVertex, VoronoiEdge, VoronoiFace
}
```

---

## Testing Strategy

### Test Categories

1. **Unit Tests** (port from Spade's Rust tests)
   - Geometric predicates
   - Data structure operations
   - Algorithm correctness

2. **Property-Based Tests**
   - Delaunay property holds
   - Mesh quality constraints satisfied
   - Voronoi duality

3. **Integration Tests**
   - End-to-end triangulation
   - Compare with Triangle.NET
   - Performance benchmarks

4. **Comparison Tests**
   - Run same inputs through Rust Spade and C# port
   - Compare outputs (should be identical or very similar)

### Test Structure

```
tests/
├── Spade.Tests/
│   ├── Primitives/
│   │   ├── Point2Tests.cs
│   │   └── CoordinateTests.cs
│   ├── DCEL/
│   │   ├── DCELTests.cs
│   │   └── TopologyTests.cs
│   ├── Triangulation/
│   │   ├── DelaunayTests.cs
│   │   ├── InsertionTests.cs
│   │   └── BulkLoadTests.cs
│   ├── Refinement/
│   │   ├── RefinementTests.cs
│   │   ├── AngleConstraintTests.cs
│   │   └── AreaConstraintTests.cs
│   ├── CDT/
│   │   └── ConstrainedDelaunayTests.cs
│   ├── Voronoi/
│   │   └── VoronoiExtractionTests.cs
│   └── Integration/
│       ├── EndToEndTests.cs
│       └── PerformanceTests.cs
```

### Test Framework

- **xUnit** - Primary test framework
- **FluentAssertions** - Readable assertions
- **BenchmarkDotNet** - Performance testing

---

## Performance Considerations

### Expected Performance Targets

| Operation | Rust Spade | C# Port Target | Acceptable Range |
|-----------|-----------|---------------|------------------|
| Basic Delaunay (10k pts) | 100ms | 150-200ms | < 250ms |
| With Refinement (10k pts) | 250ms | 400-500ms | < 600ms |
| Voronoi Extraction | 20ms | 30-50ms | < 60ms |
| Memory (10k pts) | 3 MB | 4-5 MB | < 7 MB |

### Optimization Strategy

1. **Phase 1-4:** Focus on correctness, not performance
2. **Phase 5:** Profile and optimize:
   - Hot paths
   - Memory allocations
   - Cache locality
   - SIMD (if beneficial)

### Performance Tools

- **BenchmarkDotNet** - Accurate benchmarking
- **dotTrace** - Profiling
- **PerfView** - Memory analysis

---

## Documentation Standards

### Code Documentation

**XML Documentation Comments:**
```csharp
/// <summary>
/// Inserts a new vertex into the triangulation.
/// </summary>
/// <param name="point">The 2D point to insert.</param>
/// <returns>A handle to the newly inserted vertex.</returns>
/// <exception cref="InsertionException">
/// Thrown if the point is outside valid coordinate range.
/// </exception>
/// <remarks>
/// This method uses incremental insertion algorithm.
/// See Guibas & Stolfi (1985) for details.
///
/// Ported from: spade/src/delaunay_triangulation.rs:insert()
/// </remarks>
public VertexHandle Insert(Point2<S> point)
{
    // ...
}
```

### Porting Notes

**Track every porting decision:**
```csharp
// PORTING NOTE: Rust uses Vec<T>, C# uses List<T>.
// Both are dynamic arrays, semantics are identical.
private List<Vertex> vertices = new();

// PORTING NOTE: Rust's Option<T> → C#'s T? (nullable).
// null represents None, value represents Some(value).
private Vertex? FindVertex(int id) { /* ... */ }

// PORTING NOTE: Rust's Result<T, E> → C# exceptions.
// Throwing InsertionException instead of returning Result.
public VertexHandle Insert(Point2<S> point)
{
    if (!IsValidCoordinate(point))
        throw new InsertionException("Invalid coordinate");
    // ...
}
```

---

## Build Configuration

### Project File Structure

**Spade.csproj:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net8.0;net9.0</TargetFrameworks>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>

    <PackageId>Spade</PackageId>
    <Version>1.0.0</Version>
    <Authors>Spade Contributors</Authors>
    <Description>
      High-quality Delaunay triangulation and Voronoi diagrams for .NET.
      Ported from the Rust Spade library.
    </Description>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/your-org/spade-port</PackageProjectUrl>
    <RepositoryUrl>https://github.com/your-org/spade-port</RepositoryUrl>
    <PackageTags>delaunay;voronoi;triangulation;mesh;geometry</PackageTags>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="System.Runtime.CompilerServices.Unsafe" Version="6.0.0" />
  </ItemGroup>
</Project>
```

---

## Acceptance Criteria

### Phase Completion Checklist

Each phase must meet these criteria before moving to the next:

✅ **Code Complete**
- All components implemented
- No compiler warnings
- Follows C# conventions

✅ **Tests Passing**
- All unit tests pass
- Integration tests pass
- No regressions

✅ **Documentation**
- XML doc comments complete
- Porting notes documented
- Examples provided

✅ **Review**
- Code reviewed
- Architecture validated
- Performance acceptable

### Final Acceptance (Phase 5)

✅ **Functional:**
- All Spade features working
- API compatible with Triangle.NET usage
- No known bugs

✅ **Performance:**
- Within 2x of Rust Spade
- Faster than Triangle.NET (or comparable)
- Memory usage acceptable

✅ **Quality:**
- 80%+ code coverage
- All tests pass
- Documentation complete

✅ **Integration:**
- fantasy-map-generator uses new library
- Visual output identical
- No regressions

---

## Risk Management

### Identified Risks

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| **Algorithm complexity** | Medium | High | Break into small steps, extensive testing |
| **Performance issues** | Low | Medium | Profile early, optimize iteratively |
| **Rust patterns hard to port** | Medium | Medium | Adapt to C# idioms, don't copy blindly |
| **Handle system complexity** | Medium | Medium | Simplify if needed (use indices) |
| **Numerical precision** | Low | High | Port exact predicates carefully |
| **Timeline overrun** | Medium | Low | Focus on core features first, optimize later |

---

## Success Metrics

### Quantitative Metrics

- ✅ **Correctness:** 100% of tests passing
- ✅ **Coverage:** >80% code coverage
- ✅ **Performance:** Within 2x of Rust Spade
- ✅ **Quality:** <5 bugs per 1000 lines
- ✅ **Documentation:** 100% public API documented

### Qualitative Metrics

- ✅ Code is readable and maintainable
- ✅ Architecture is clean and modular
- ✅ Follows .NET best practices
- ✅ Other developers can contribute easily

---

## Next Steps

### Immediate Actions

1. ✅ **Set up project structure** (done - directory exists)
2. ➡️ **Create detailed phase RFCs** (RFC-002 through RFC-006)
3. ➡️ **Clone Spade reference** (`ref-projects/spade`)
4. ➡️ **Set up .NET solution** (`dotnet/Spade.sln`)
5. ➡️ **Begin Phase 1** (Core Data Structures)

### Reading List

**Before starting, read:**
1. Spade's `Architecture.md`
2. Spade's API documentation
3. Guibas & Stolfi (1985) - "Primitives for manipulation of general subdivisions"
4. Cheng, Dey, Shewchuk (2013) - "Delaunay Mesh Generation"

---

## References

### Source Materials

- **Spade (Rust):** https://github.com/Stoeoef/spade
- **Spade Documentation:** https://docs.rs/spade
- **Spade Architecture:** https://github.com/Stoeoef/spade/blob/master/Architecture.md

### Academic References

- Guibas & Stolfi (1985). "Primitives for the manipulation of general subdivisions and the computation of Voronoi diagrams"
- Ruppert (1995). "A Delaunay Refinement Algorithm for Quality 2-Dimensional Mesh Generation"
- Cheng, Dey, Shewchuk (2013). "Delaunay Mesh Generation"

### Related RFCs

- **RFC-002:** Phase 1 - Core Data Structures
- **RFC-003:** Phase 2 - Basic Delaunay Triangulation
- **RFC-004:** Phase 3 - Mesh Refinement
- **RFC-005:** Phase 4 - Constrained Delaunay & Voronoi
- **RFC-006:** Phase 5 - Integration & Testing

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 0.1 | 2025-01-20 | AI Agent | Initial draft |

---

## Appendix: Quick Reference

### Key Rust → C# Patterns

```rust
// Rust: Struct with methods
pub struct Point2<S> { pub x: S, pub y: S }
impl<S> Point2<S> { pub fn new(x: S, y: S) -> Self { Self { x, y } } }
```
```csharp
// C#: Struct with constructor
public struct Point2<S> {
    public S X { get; set; }
    public S Y { get; set; }
    public Point2(S x, S y) { X = x; Y = y; }
}
```

```rust
// Rust: Trait
pub trait SpadeNum: Copy + PartialOrd { }
```
```csharp
// C#: Interface
public interface ISpadeNum : IComparable<T> { }
```

```rust
// Rust: Enum with data
pub enum Result<T, E> { Ok(T), Err(E) }
```
```csharp
// C#: Abstract class or discriminated union
public abstract class Result<T, E> { }
public class Ok<T, E> : Result<T, E> { public T Value { get; } }
public class Err<T, E> : Result<T, E> { public E Error { get; } }
```

---

**END OF RFC-001**
