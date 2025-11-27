# RFC-017: Spade .NET Natural Neighbor – Fast Algorithm & Next Steps

**Status:** Draft  
**Phase:** Post-robustness (RFC-016), pre-optimisation/thread-safety  
**Scope:** `spade-port/dotnet/src/Spade/NaturalNeighborInterpolator.cs` and grid helpers under `Spade.Advanced.Interpolation`  
**Complexity:** ⭐⭐⭐ Medium  
**Dependencies:** RFC-009 (validation with Julia oracle), RFC-011 (robust predicates), RFC-016 (natural neighbor robustness)

---

## 1. Context & Summary of Findings

This RFC records the current state of the **natural neighbor interpolation** implementation in the Spade .NET port and how it relates to the class of methods usually described as a **fast Delaunay-based Sibson algorithm** (e.g., Liang & Hale, Tinfour).

### 1.1 Files and types involved

- `dotnet/src/Spade/NaturalNeighborInterpolator.cs`
  - `NaturalNeighborInterpolator<V, DE, UE, F, L>` – core Sibson-style interpolator.
- `dotnet/src/Spade/DelaunayTriangulationExtensions.cs`
  - `NaturalNeighbor()` extension constructor.
- `dotnet/src/Spade.Advanced/Interpolation/GridNaturalNeighbor2D.cs`
  - Builds a Delaunay triangulation and uses `NaturalNeighbor()` on a regular grid.
- `dotnet/src/Spade.Advanced/Interpolation/NaturalNeighborGrid2D.cs`
  - `Exact(...)` – grid wrapper around `GridNaturalNeighbor2D.InterpolateToGrid`.
  - `Discrete(...)` – grid wrapper around `DiscreteGridNaturalNeighbor2D` (KD-tree approximation).
- `dotnet/src/Spade.Advanced/Interpolation/DiscreteGridNaturalNeighbor2D.cs`
- `dotnet/src/Spade.Advanced/Interpolation/NaturalNeighborGrid3D.cs`
- `dotnet/src/Spade.Advanced/Interpolation/DiscreteGridNaturalNeighbor3D.cs`

### 1.2 High-level finding

The existing `NaturalNeighborInterpolator` already implements a **fast, Delaunay-based Sibson natural neighbor algorithm** of the same general form as the one described in:

- Tinfour documentation, "A Fast Algorithm for Natural Neighbor Interpolation".
- Liang & Hale, *A stable and fast implementation of natural neighbor interpolation*.

Key points:

- It uses a **pre-built Delaunay triangulation** and never mutates it during interpolation.
- For each query, it constructs a **Bowyer–Watson-style envelope** around the query point using an edge-inspection procedure and an in-circle predicate.
- It then computes weights by integrating area contributions built from **circumcenters** and **shoelace (Gauss) area sums**, normalised to give Sibson weights.

The conclusion is that there is no missing “fast algorithm” to add; instead, work now focuses on:

- Clarifying and documenting that the implementation is already a fast variant.
- Auditing for **thread-safety** and possible micro-optimisations.
- Adding **cross-library tests** against a reference implementation (e.g., Julia `DelaunayTriangulation.jl` or Tinfour outputs) to validate behaviour.

---

## 2. Algorithm Mapping: Spade .NET vs. Fast Delaunay-Based Sibson

This section documents how the current implementation matches the standard fast algorithm design.

### 2.1 Natural neighbor workflow in Spade .NET

For a query point `q`:

1. **Locate query in triangulation**
   - Method: `_triangulation.LocateWithHintOptionCore(position, null)`.
   - Output: `PositionInTriangulation` (on face, edge, vertex, or outside hull).

2. **Build natural neighbor edge set (Bowyer–Watson envelope)**
   - Method: `GetNaturalNeighborEdges(...)`.
   - Uses `InspectFlips(...)` to examine edges around the query using an in-circle test:
     - `MathUtils.ContainedInCircumference(v2Pos, v1Pos, v0Pos, position)`.
   - Conceptually, this matches the **Bowyer–Watson cavity** that would be formed if the query were inserted into the triangulation, but is computed without mutating the structure.

3. **Compute weights via insertion cell and areas**
   - Method: `GetNaturalNeighborWeights(...)`.
   - For each neighbour edge, it:
     - Constructs points equal to **circumcenters** of triangles in coordinates relative to `q`.
     - Accumulates contributions to polygon area via the **shoelace formula**:
       - `positiveArea += last.X * current.Y;`
       - `negativeArea += last.Y * current.X;`
       - `polygonArea = positiveArea - negativeArea;`.
   - These polygon areas are the captured Voronoi areas associated with each neighbour, up to a constant that cancels when taking differences.
   - Finally, the areas are normalised so that their sum is 1, yielding Sibson weights.

4. **Interpolation and fallbacks**
   - `Interpolate(selector, position)` multiplies weights by nodal values and sums.
   - If the natural neighbour construction fails (e.g., empty neighbour set, degenerate zero-area polygon, or non-finite results), the implementation falls back to **barycentric interpolation** where appropriate.

This is the same shape as the "fast" algorithms described in the Tinfour and Liang & Hale references: a read-only Delaunay triangulation driving a natural neighbour scheme via envelope construction and area computations.

### 2.2 Grid wrappers and discrete variants

- `NaturalNeighborGrid2D.Exact(...)` and `GridNaturalNeighbor2D.InterpolateToGrid(...)`:
  - Build a `DelaunayTriangulation<PointWithValue,...>` once.
  - Construct a `NaturalNeighborInterpolator` via the extension method.
  - Evaluate it on a grid of query points.
  - This path is "exact" in the Sibson sense (subject to numerical error and fallback logic).

- `DiscreteGridNaturalNeighbor2D`/`DiscreteGridNaturalNeighbor3D`:
  - Use KD-tree structures in grid index coordinates.
  - Implement an **approximate discrete natural neighbour** method (neighbour spreading over grid cells using nearest-sample radius), not Sibson's exact algorithm.

---

## 3. Design Implications

### 3.1 There is already a fast algorithm

Given the mapping above, the library can be documented as already providing a **fast, Delaunay-based natural neighbour interpolator**. Future work is not about adding a new algorithm from scratch, but about:

- Making performance and robustness properties more explicit.
- Improving **thread-safety** and multi-thread scalability of calls to `NaturalNeighborInterpolator`.
- Adding **regression tests** that compare against other libraries/implementations.

### 3.2 Thread-safety considerations

Current design:

- `NaturalNeighborInterpolator` stores multiple mutable buffers as **instance fields**:
  - `_inspectEdgesBuffer`
  - `_naturalNeighborEdges`
  - `_insertCellBuffer`
  - `_weightBuffer`
- These are reused across calls to avoid allocations.
- **Consequence:** a single `NaturalNeighborInterpolator` instance is **not thread-safe** for concurrent calls.

However:

- The underlying `DelaunayTriangulation` is used in a read-only fashion by the interpolator.
- Therefore, the triangulation itself is compatible with multi-threaded use, as long as each thread uses separate buffers/state.

There are at least two viable designs for thread-safe fast interpolation:

1. **Per-thread interpolator instance (simple)**
   - Each thread creates its own `NaturalNeighborInterpolator` on the shared triangulation.
   - No shared mutable state inside the interpolator.

2. **Stateless/static computation API (more advanced)**
   - Move scratch buffers to local variables or to a separate context object that is explicitly passed per call or per thread.
   - Provide a functional-style static API that accepts the triangulation, query, and buffers.

This RFC proposes implementing (1) in the short term, and evaluating (2) as a possible optimisation if allocations or construction costs become relevant.

---

## 4. Planned Work

This section outlines concrete follow-up items building on this finding.

### 4.1 Audit and document thread-safety behaviour

**Goal:** Make guarantees (and non-guarantees) around thread-safety explicit, and ensure the most common multi-threaded patterns are supported efficiently.

**Tasks:**

- Document in XML docs and/or higher-level docs that:
  - `NaturalNeighborInterpolator` instances are **not thread-safe for concurrent use**.
  - It is safe for multiple instances to share a single `DelaunayTriangulation` as long as that triangulation is not mutated.
- Add a small helper/factory pattern (if useful) encouraging per-thread instances, e.g.:
  - Using `triangulation.NaturalNeighbor()` in each thread or within a parallel loop.
- Optionally explore a refactor where scratch buffers are grouped into a dedicated context object so that the main interpolator code can be reused with per-thread contexts.

### 4.2 Optional: Introduce an explicitly thread-safe variant

**Goal:** Provide a type/API that is clearly safe to call from multiple threads without user having to reason about buffer reuse.

**Sketch options (to be decided in a follow-up RFC or PR):**

- `ThreadSafeNaturalNeighborInterpolator` wrapper that:
  - Holds a shared `DelaunayTriangulation`.
  - Uses thread-local or pooled scratch state internally.
- Or, static methods like `NaturalNeighborInterpolator.ComputeWeights(triangulation, position, scratchState)` where `scratchState` is a user-managed buffer object that can be owned per thread.

Any such type should be thin over the existing implementation to avoid divergence.

### 4.3 Add cross-library comparison tests

**Goal:** Validate that the Spade .NET implementation matches a reference implementation within a reasonable tolerance.

**Candidate references:**

- Julia `DelaunayTriangulation.jl` (already referenced in RFC-009 as an oracle).
- Tinfour (Java), via exported datasets or offline precomputed results.

**Possible approach:**

1. Use a fixed set of sample points and query points (e.g., grids, random clouds, structured diagnostic cases).
2. For each, record:
   - Natural neighbour weights per vertex for a subset of queries, and/or
   - Interpolated scalar values for simple fields (planar, quadratic, etc.).
3. Compare the Spade .NET outputs against the oracle values:
   - Use max absolute error and/or relative error thresholds.
   - Ensure sign patterns and weight support sets match where appropriate.

This work can live under `dotnet/tests/Spade.Tests/`, potentially extending the work described in RFC-009.

### 4.4 Performance profiling and micro-optimisations

**Goal:** Confirm that the current implementation is competitive for typical workloads and identify any easy wins.

**Tasks (optional / stretch):**

- Benchmark:
  - Grid interpolation via `NaturalNeighborGrid2D.Exact`.
  - Direct calls to `NaturalNeighborInterpolator.Interpolate` on large point sets and many query points.
- Look for hotspots such as:
  - Repeated `MathUtils.Circumcenter` calls.
  - Allocation patterns in neighbour edge discovery.
- Consider minor optimisations only if they do not compromise clarity or robustness.

---

## 5. Risks and Open Questions

- **Numerical parity vs. other implementations:**
  - Exact bit-level parity with Tinfour or Liang & Hale is unlikely or unnecessary; instead, we should target robust agreement up to floating-point tolerances.
- **API surface for thread-safety:**
  - We must avoid overcomplicating the public API. A minimal clarification that instances are not thread-safe may be enough for many users.
- **Maintenance cost:**
  - Any new thread-safe wrapper or context object should be carefully designed to avoid duplicating logic from `NaturalNeighborInterpolator`.

---

## 6. Decision & Next Actions

**Decision:**

- Treat `NaturalNeighborInterpolator` as the library's implementation of a **fast, Delaunay-based natural neighbour interpolation algorithm**.
- Do **not** introduce a completely new algorithm; instead, improve documentation, thread-safety guidance, and validation.

**Immediate next actions (for follow-up PRs):**

1. Document thread-safety characteristics and recommended usage patterns for `NaturalNeighborInterpolator` and grid helpers.
2. Add a limited set of cross-library comparison tests against an oracle implementation.
3. Optionally design and prototype a small, explicit thread-safe wrapper or context mechanism if real-world workloads demand it.
