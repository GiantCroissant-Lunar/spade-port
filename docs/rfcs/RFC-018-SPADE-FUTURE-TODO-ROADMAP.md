# RFC-018: Spade .NET – Future TODO Roadmap (Performance, Constraints, Determinism)

**Status:** Draft
**Phase:** Post-port completion (RFC-012) / ongoing quality & performance
**Scope:** `spade-port/dotnet/src/Spade/*`, `spade-port/dotnet/src/Spade.Advanced/*`
**Motivation:** Consolidate “next improvements” inspired by `ref-projects/` into an implementable backlog.

---

## 1. Context

Spade .NET is already feature-rich (Delaunay, constrained Delaunay, refinement, Voronoi, clipped Voronoi, natural neighbor).
The remaining work is largely about:

- Throughput and allocation reduction for large point sets.
- Broadening domain support (holes / non-convex clipping) while preserving robustness.
- Tightening determinism and indexing contracts for downstream consumers (e.g., worldgen pipelines).

This RFC collects concrete TODOs based on the reference corpus under:

- `ref-projects/delaunator`
- `ref-projects/DelaunayTriangulation.jl`
- `ref-projects/Triangle.NET`
- `ref-projects/voronator-sharp`
- `ref-projects/robust`, `ref-projects/robust-predicates`
- `ref-projects/RobustGeometry.NET`
- (and related: `Fantasy-Map-Generator`, natural neighbor repos)

---

## 2. Goals / Non-goals

### Goals

- Make large triangulation builds faster and more predictable (time + GC).
- Improve clipped Voronoi determinism and indexing consistency (1 cell per generator; stable ordering).
- Enable future support for non-convex domains and holes without “coverage holes”.
- Add oracle-driven validation and regression tests across implementations.

### Non-goals (for this RFC)

- Rewrite core DCEL/topology structures.
- Implement full general polygon boolean ops in Spade (unless necessary for clipping with holes).

---

## 3. Proposed Work Items

### 3.1 Delaunator-inspired bulk build path (Performance)

**Reference:** `ref-projects/delaunator`

Observations from Delaunator-style implementations:

- Prefer a “bulk build” that operates on flat arrays (SoA) rather than per-insert object churn.
- Keep explicit hull tracking / hull hash for fast point location during incremental construction.

TODOs:

- Add (or harden) a `BulkInsert`/`BuildFromPoints` API that:
  - Accepts `ReadOnlySpan<Point2<double>>` or `IReadOnlyList<TVertex>` and preallocates.
  - Minimises per-insert allocations and reduces dictionary/set churn.
- Profile and document hot paths:
  - vertex insertion, edge flips, locate-with-hint.
- Consider a “fast path” when the input is pre-shuffled deterministically (seeded shuffle) to avoid worst-case insertion.

Acceptance signals:

- Repeatable benchmark for `N=10k..200k` points showing throughput and GC improvements.

### 3.2 Deterministic indexing and tie-breaking (Integration correctness)

**Reference:** `ref-projects/voronator-sharp`

Problem class:

- In consumer code, a Voronoi “cell” must be reliably attributable to the generator site index.
- Edge cases (degenerate cells, near-collinear sites) must not silently reorder or drop cells without a clear failure mode.

TODOs:

- Document and enforce a contract: **generator id ↔ result cell index**.
- Ensure clipped Voronoi outputs:
  - Return cells in stable generator-id order.
  - Provide explicit diagnostics when a cell is degenerate/missing.
- Audit and standardise deterministic tie-breakers:
  - equal-distance cases, collinear/near-collinear orientation results.

Acceptance signals:

- Repeat runs with identical input produce identical cell ordering and polygons (within epsilon).

### 3.3 Non-convex domains and holes (Future clipping)

**Reference:** `ref-projects/Triangle.NET`, `ref-projects/DelaunayTriangulation.jl`

Current `ClipPolygon` is documented as convex/CCW and the robust clipped Voronoi approach intersects half-planes.
That approach is excellent for convex domains; non-convex domains and holes are a different problem.

TODOs (future direction options):

- Option A: Triangulate the domain (with constraints/holes), then assemble cells by intersecting with triangle sets.
- Option B: Add “multi-domain clipping” by computing Voronoi cell polygons (possibly unbounded), then clipping using a polygon clipping library.

Acceptance signals:

- A small reference suite of non-convex+holes domains with coverage verification (no gaps, no overlaps beyond epsilon).

### 3.4 Oracle-driven validation (Correctness)

**References:** `ref-projects/DelaunayTriangulation.jl`, `ref-projects/robust-predicates`

TODOs:

- Add cross-library test vectors:
  - random seeds + adversarial near-degenerate configurations.
  - compare topological invariants (triangle adjacency / hull / neighbor sets) against Julia.
- Expand predicate regression tests:
  - targeted inputs for `orient2d`/`incircle` sign stability.

Acceptance signals:

- A reproducible validation harness that can be run locally and in CI.

---

## 4. Notes for fanta-world Integration

Downstream consumers (e.g., hierarchical subdivision) strongly prefer:

- **One polygon per site** and stable mapping to `siteIndex`.
- **Convex, CCW polygons** when the domain is convex.
- Deterministic behavior under seeded RNG.

This RFC’s determinism + bulk build items are the primary enablers for replacing geometry backends without regressions.

---

## 5. Open Questions

- Should the bulk-build path be a separate triangulation type (specialised for `double`) or an optional build mode?
- Do we want to support holes via constrained triangulation assembly (Spade-native) or via an external polygon clipping backend?

