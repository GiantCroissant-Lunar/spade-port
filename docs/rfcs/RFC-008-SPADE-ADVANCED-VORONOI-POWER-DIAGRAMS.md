# RFC-008: Spade Advanced Voronoi & Power Diagrams

**Status:** Draft
**Phase:** Post-port enhancements
**Complexity:** ⭐⭐⭐ Hard
**Dependencies:** RFC-001 (Master Porting Strategy), RFC-005 (CDT & Voronoi), RFC-033 (Spade Performance Benchmarks)
**External References:**
- Rust Spade (original algorithms)
- DelaunayTriangulation.jl (Julia) - MIT licensed
- Cheng, Dey, Shewchuk - *Delaunay Mesh Generation*

---

## Goal

Define and scope **advanced diagram features** for Spade on .NET that go beyond the core Rust Spade feature set, informed by the Julia `DelaunayTriangulation.jl` package:

- Clipped Voronoi diagrams (clipped to convex or polygonal domains)
- Centroidal / smoothed Voronoi diagrams
- Weighted Delaunay triangulations
- Power diagrams (weighted Voronoi)

This RFC is **design only**. No implementation is required for acceptance; it serves as a contract for later implementation RFCs.

---

## Motivation

Spade on .NET already targets:

- Unconstrained and constrained Delaunay triangulations
- Mesh refinement
- Voronoi extraction (dual of triangulation)

The **Julia DelaunayTriangulation.jl** library demonstrates a rich set of **advanced constructions**:

- Clipped and centroidal Voronoi tessellations
- Weighted triangulations and power diagrams
- Curve-bounded and disjoint domains

Porting the *entire* Julia feature set is out of scope, but selectively adopting and adapting some of these ideas would:

- Improve expressiveness for consumers (e.g., FMG, other geometry-heavy apps)
- Provide more realistic and challenging benchmark scenarios
- Showcase Spade as a modern, production-ready geometry toolkit on .NET

---

## Non-Goals

- Porting DelaunayTriangulation.jl wholesale into C#.
- Replacing Rust Spade as the **source of truth** for core triangulation logic.
- Supporting 3D or non-planar meshes.
- Building PDE solvers or domain-specific applications on top of Spade.

---

## High-Level Design

### Namespaces & Assemblies

Two options exist for where these features live:

1. **Core Spade assembly**
   - Namespaces: `Spade.Voronoi.Advanced`, `Spade.Power`
   - Pros: single package; simple dependency graph
   - Cons: mixes core port with extras, harder to track parity with Rust

2. **Spade.Advanced / Extensions assembly (preferred, see RFC-010)**
   - Project: `dotnet/src/Spade.Advanced/Spade.Advanced.csproj`
   - Namespaces: `Spade.Advanced.Voronoi`, `Spade.Advanced.Power`
   - Pros: clean separation of responsibilities; opt-in for advanced features
   - Cons: additional project and NuGet package to maintain

**This RFC assumes Option 2 (Spade.Advanced), but the final decision is captured in RFC-010.**

### Conceptual Building Blocks

All advanced features should be implemented **on top of** the existing Spade triangulation and Voronoi primitives:

- Use `DelaunayTriangulation<TVertex, TEdge, TFace>` as the base structure.
- Use existing Voronoi extraction as the starting point for advanced operations.
- Use the same numeric types and predicates as core Spade.

DelaunayTriangulation.jl is treated as:

- A **conceptual reference** for algorithms and interfaces.
- A **validation oracle** for small to medium-sized test cases.

We do **not** re-implement Julia's data structures 1:1.

---

## Feature Areas

### 1. Clipped Voronoi Diagrams

**Goal:** Clip the Voronoi diagram to a specified convex (or simple polygonal) domain.

**Input:**
- Existing triangulation or point set
- Clip domain (e.g., convex polygon, circle approximation)

**Output:**
- Voronoi cells intersected with the domain

**Design Notes:**
- Built on top of existing Voronoi extraction.
- Clipping operation can be expressed in terms of polygon clipping of Voronoi cells.
- DelaunayTriangulation.jl examples:
  - Clipping to circles or generic convex polygons via boundary nodes.

**API Sketch (illustrative):**

```csharp
namespace Spade.Advanced.Voronoi;

public static class ClippedVoronoi
{
    public static ClippedVoronoiDiagram ClipToPolygon<TVertex, TEdge, TFace>(
        DelaunayTriangulation<TVertex, TEdge, TFace> triangulation,
        IReadOnlyList<Point2<double>> polygon)
    {
        // Implementation TBD (RFC only)
        throw new NotImplementedException();
    }
}
```

### 2. Centroidal / Smoothed Voronoi

**Goal:** Provide an algorithmically principled way to compute **centroidal Voronoi tessellations (CVT)**, where generators lie at the centroids of their cells.

We already perform Lloyd relaxation in the FMG port using `GeometryUtils.ApplyLloydRelaxation`. This RFC explores:

- Potential alignment with centroidal Voronoi constructions
- Improved convergence behavior based on Julia examples

**Design Notes:**
- Implemented as higher-level algorithms that:
  - Iterate: triangulation → Voronoi → centroid computation → point update.
  - Provide tuning parameters for iterations, convergence thresholds.
- Does not need to be part of the core Spade library; ideal for `Spade.Advanced`.

### 3. Weighted Delaunay & Power Diagrams

**Goal:** Support **weighted triangulations** and their dual **power diagrams**.

**Motivation:**
- Weighted diagrams appear in interpolation, sampling, and various scientific/engineering workflows.
- DelaunayTriangulation.jl already exposes weighted triangulation and power diagrams.

**Design Options:**

1. Extend existing triangulation types with weights.
2. Introduce separate weighted triangulation types (preferred for clarity):
   - `WeightedDelaunayTriangulation<TVertex, TEdge, TFace>`
   - `PowerDiagram`

**API Sketch (illustrative):**

```csharp
namespace Spade.Advanced.Power;

public sealed class WeightedDelaunayTriangulation<TVertex, TEdge, TFace>
{
    // Wrap or compose a core Spade triangulation
}

public static class PowerDiagramBuilder
{
    public static PowerDiagram Build(
        IReadOnlyList<Point2<double>> points,
        IReadOnlyList<double> weights)
    {
        // Implementation TBD (RFC only)
        throw new NotImplementedException();
    }
}
```

---

## Interactions with DelaunayTriangulation.jl

DelaunayTriangulation.jl provides **reference implementations** for:

- Centroidal Voronoi diagrams (e.g., `centroidal_smooth`, `voronoi(..., smooth=true)`).
- Clipped Voronoi diagrams with generic convex polygons.
- Weighted triangulations and power diagrams.

This RFC proposes the following interaction model:

1. **Study & document** the relevant Julia functions and their invariants.
2. **Design .NET APIs** that provide equivalent capabilities, mapped onto Spade's data model.
3. Use Julia as a **testing oracle** for selected scenarios (see RFC-009).
4. Keep all C# implementations idiomatic and compatible with Spade's performance and robustness goals.

---

## Implementation Plan (High Level)

This RFC does **not** require implementation to be completed. Instead, it defines phases for future implementation RFCs.

### Phase A: Design & Scoping

- [ ] Complete feature matrix comparing Spade, spade-port, and DelaunayTriangulation.jl.
- [ ] Finalize which advanced features to implement first (likely clipped Voronoi + power diagrams).
- [ ] Define minimal APIs and namespaces for v1.

### Phase B: Prototyping & Validation

- [ ] Implement internal prototypes using Spade's triangulation and Voronoi extraction.
- [ ] Validate against small to medium point sets using DelaunayTriangulation.jl as oracle (see RFC-009).
- [ ] Measure performance impact using RFC-033 benchmarks.

### Phase C: Public API & Documentation

- [ ] Finalize public types and methods in `Spade.Advanced`.
- [ ] Add usage examples to `USAGE.md` and dedicated docs.
- [ ] Ensure XML documentation and porting notes reference DelaunayTriangulation.jl where appropriate.

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|-----------|
| Over-scoping advanced features | High | Start with 1–2 high-value features; defer others |
| Divergence from Rust Spade semantics | Medium | Keep all advanced features in `Spade.Advanced`; core remains faithful |
| Performance regressions | Medium | Use RFC-033 benchmarks to compare against baseline |
| Numerical robustness | High | Align predicates and invariants with both Spade and DelaunayTriangulation.jl |

---

## Acceptance Criteria

- ✅ Clear list of advanced features to target in v1 (clipped Voronoi, centroidal Voronoi, power diagrams).
- ✅ Agreed namespace and assembly strategy (subject to RFC-010 decision).
- ✅ Documented interaction model with DelaunayTriangulation.jl as conceptual and testing reference.
- ✅ Phased plan for prototyping, validation, and public API design.
