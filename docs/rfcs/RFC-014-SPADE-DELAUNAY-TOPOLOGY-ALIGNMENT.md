# RFC-014: Spade .NET Delaunay Topology Alignment

**Status:** Draft  
**Phase:** Post-DCEL realignment / topology parity  
**Scope:** `spade-port/dotnet/src/Spade` Delaunay core + topology tests in `Spade.Tests`  
**Complexity:** ⭐⭐⭐⭐ Hard  
**Dependencies:** RFC-012 (Port Completion), RFC-013 (DCEL & Hull Realignment)

---

## 1. Motivation

After RFC-013, the DCEL, hull handling, and refinement are in good shape:

- Refinement tests pass (`Refine_WithMaxArea_SplitsLargeTriangle`, `Refine_WithConstraintEncroachment_SplitsConstraintAndStopsRefining`).
- Outer-face tagging and `InnerFaces()` semantics match the Rust refinement behaviour.

However, several **topology tests** still fail:

- `VoronoiTests.DelaunayTopology_Matches_OriginalSpade_On_3x3_Grid`
- `VoronoiTests.DelaunayTopology_SpadeVsNts_On_RectangleWithCenter`
- `DelaunayOracleTests.DelaunayTopology_SpadeVsNts_On_RectangleWithCenter`

Symptoms:

- The central vertex in the 3×3 grid is not adjacent to all four cardinal neighbors `{1,3,5,7}` as expected.
- For the rectangle-with-center scenario, adjacency vs NetTopologySuite (NTS) and vs the Rust spade oracle still differs.

Given that DCEL invariants and refinement are now correct, these mismatches point to **insertion and legalization behaviour** (especially edge flipping and tie-breaking), not to gross structural bugs.

The goal of this RFC is to realign the C# Delaunay triangulation’s **topology** (adjacency graph) with Rust spade and the test oracles for the configurations we care about.

---

## 2. Goals

By the end of this RFC:

- **G1: Delaunay topology parity on small structured grids.**
  - `DelaunayTopology_Matches_OriginalSpade_On_3x3_Grid` passes:
    - The central site (index 4) is adjacent to `{1,3,5,7}`.
    - Those neighbors see `4` as a neighbor.
  - `DelaunayTopology_SpadeVsNts_On_2x2_Grid` remains green.

- **G2: Delaunay topology parity on rectangle-with-center.**
  - `VoronoiTests.DelaunayTopology_SpadeVsNts_On_RectangleWithCenter` passes.
  - `DelaunayOracleTests.DelaunayTopology_SpadeVsNts_On_RectangleWithCenter` passes.

- **G3: Legalization and flip logic are correct and robust.**
  - `LegalizeEdge` decisions match Rust’s `legalize_edge` for our scenarios:
    - Edges are flipped only when they truly violate the Delaunay condition.
    - Non-convex quads are not flipped accidentally.

- **G4: No regression of existing passing tests.**
  - Refinement tests and DCEL validation remain green.
  - Natural neighbor tests (`GradientInterpolationTests`) remain stable.

---

## 3. Non-Goals

- Forcing bit-for-bit identical triangulations to Rust spade in *all* inputs; we only require parity for:
  - The explicit test configurations (grids, rectangles with center).
  - “Normal” FMG-style random inputs.
- Changing public API signatures of `DelaunayTriangulation` or `TriangulationBase`.
- Introducing new features beyond standard Delaunay/CDT and Voronoi support.

---

## 4. Design Overview

The remaining mismatches arise from **local decisions in the triangulation algorithm**, in particular:

- How a new vertex is inserted when it falls outside the convex hull (`InsertOutsideOfConvexHull`).
- How edges are legalized (`LegalizeEdge` and `FlipCw`), including orientation and `ContainedInCircumference` usage.
- How ties are broken when multiple valid triangulations exist (e.g., diagonals in a square or grid cell).

To align behaviour, we will:

1. **Instrument and compare** the C# triangulation against Rust spade for small fixed inputs, capturing edge sets and adjacent faces.
2. **Verify and, if necessary, adjust**:
   - The geometric predicates used by `LegalizeEdge` (`MathUtils.ContainedInCircumference` / `RobustPredicates.Incircle`).
   - The edge flipping conditions and orientation order.
   - Hull insertion behaviour for points in the interior vs outside the hull (to avoid subtle orientation differences at the boundary).
3. **Use the tests as an oracle** for our target cases, rather than chasing general “theoretical” equivalence.

---

## 5. Detailed Work Items

### 5.1. Capture and compare adjacency for 3×3 grid and rectangle-with-center

**Goal:** Make the topology mismatches concrete and reproducible, and provide a debugging harness.

**Tasks:**

1. **Add debug logging helpers (behind env flags) for adjacency:**
   - In `VoronoiTests` and `DelaunayOracleTests`, add an optional trace (already partly present) that prints:
     - For each site index: sorted list of neighbors in `spadeAdjacency` and `ntsAdjacency` (where applicable).
   - Ensure these logs only emit when an env var like `SPADE_DIAG_TOPOLOGY=1` is set.

2. **Create a small C# harness (test or console) that:**
   - Builds the same 3×3 grid and rectangle-with-center input as the tests.
   - Generates the C# adjacency map via `DirectedEdges()`.
   - Dumps the edge set (as undirected pairs `(i,j)`, sorted with `i<j`) to a text file.

3. **Run the equivalent scenario in Rust spade** (using `ref-projects/spade`):
   - For the same point sets and insertion order, dump the Rust adjacency as `(i,j)` pairs.

4. **Compare the edge sets:**
   - Identify edges present in C# but not in Rust (or NTS), and vice versa.
   - Focus on edges incident to the center vertex or the rectangle “center” point, since those are highlighted by the failing tests.

**Exit criteria:** We have concrete examples of edges that differ between C# and Rust/NTS for the tested inputs, with logs checked in (guarded by env flags).

---

### 5.2. Verify and align `LegalizeEdge` with Rust’s `legalize_edge`

**Goal:** Ensure the edge flip decisions are correct and stable.

**Tasks:**

1. **Review Rust’s `legalize_edge` implementation** in `ref-projects/spade/src/delaunay_core/triangulation_ext.rs`:
   - Confirm the exact order of arguments passed to `contained_in_circumference` (which vertices correspond to `v0..v3`).
   - Note any additional conditions (e.g., convexity checks, constraints, hull edges) that gate flipping.

2. **Confirm `MathUtils.ContainedInCircumference` semantics:**
   - For a simple test triangle and a point:
     - Check that calling `ContainedInCircumference(v0, v1, v2, v_inside)` returns `true` for a point inside the circumcircle and `false` outside.
   - If necessary, add unit tests to `MathUtilsTests` to lock in these semantics.

3. **Align the call in `TriangulationBase.LegalizeEdge`:**
   - Ensure the points passed to `ContainedInCircumference` are in the same conceptual order as Rust (up to orientation, given the incircle predicate behaviour).
   - Keep the early-outs:
     - Skip edges incident to outer faces (`face.IsOuter || revFace.IsOuter`).
     - Skip edges marked as constraints (`IsConstraint`).

4. **Optional safety checks:**
   - In DEBUG builds, for each candidate flip:
     - Validate that the quad is convex (e.g., by checking the orientation of the two triangles) before flipping.
     - If non-convex, skip the flip and log a diagnostic line under `SPADE_DIAG_TOPOLOGY`.

**Exit criteria:** For the 3×3 and rectangle-with-center inputs, edge flip logs (when enabled) show consistent decisions, and no spurious flips happen on non-convex quads.

---

### 5.3. Audit hull insertion and interior insertion paths

**Goal:** Ensure that insertion into the hull and interior matches Rust’s behaviour, especially around the convex hull.

**Tasks:**

1. **Re-read Rust’s `insert_outside_of_convex_hull`** in `triangulation_ext.rs`:
   - Note how the hull is extended, and which edges are legalized in which order.

2. **Compare with `TriangulationBase.InsertOutsideOfConvexHull`:**
   - Confirm that:
     - The initial hull edge selection (`FindHullEdgeOrThrow`) matches the Rust approach (preferring an orientation with interior on the left).
     - `CreateNewFaceAdjacentToEdge` and subsequent calls to `CreateSingleFaceBetweenEdgeAndNext` follow the same pattern of hull “peeling”.
     - Legalization calls are made on the same edges (or equivalent ones) as in Rust.

3. **Check interior insertion path:**
   - `InsertIntoFace` → `DcelOperations.InsertIntoTriangle`.
   - `InsertOnEdge` → `SplitHalfEdge` / `SplitEdge` / `SplitEdgeWhenAllVerticesOnLine`.
   - Ensure that after these operations, `LegalizeVertex` receives the same set of edges to legalize as in Rust’s `legalize_vertex` (at least conceptually).

4. **Add DEBUG diagnostics:**
   - Under an env var (e.g., `SPADE_DIAG_TOPOLOGY=1`), log the sequence of edges visited and flipped when inserting each point in the test scenarios.

**Exit criteria:** Insertion sequences for the 3×3 grid and rectangle-with-center inputs are understood and, where they differ from Rust, either aligned or documented with clear reasoning.

---

### 5.4. Tie-breaking and deterministic jitter alignment

**Goal:** Ensure that when multiple valid triangulations exist (e.g., a square can be triangulated along either diagonal), we choose a consistent triangulation that matches the test oracle.

**Tasks:**

1. **Confirm jitter usage:**
   - Verify that the same deterministic jitter (`MathUtils.ApplyDeterministicJitter`) is applied consistently wherever needed (e.g., when comparing to NTS in tests).
   - Ensure that the triangulation itself effectively “sees” the same jittered point set as the test harness, or that differences are understood and controlled.

2. **Identify tie cases in the tests:**
   - For the 2×2 and 3×3 grids and rectangle-with-center, enumerate any cells where multiple triangulations are Delaunay-equivalent.

3. **Enforce a tie-breaking policy:**
   - If necessary, add a small deterministic rule (e.g., based on vertex indices or coordinates) that biases diagonal selection to match Rust/NTS in those cases.
   - Implement this bias inside the insertion/flip logic, not in the tests.

**Exit criteria:** The adjacency sets for the target tests match the expected neighbours, even in tie cases, and this behaviour is reproducible across runs.

---

### 5.5. Tests and validation

**Goal:** Close the loop by turning our understanding into concrete passing tests.

**Tasks:**

1. **Keep existing topology tests:**
   - `DelaunayTopology_Matches_OriginalSpade_On_3x3_Grid`
   - `DelaunayTopology_SpadeVsNts_On_2x2_Grid`
   - `VoronoiTests.DelaunayTopology_SpadeVsNts_On_RectangleWithCenter`
   - `DelaunayOracleTests.DelaunayTopology_SpadeVsNts_On_RectangleWithCenter`

2. **Add focused tests if needed:**
   - Small bespoke configurations isolating a single suspect edge or flip decision (e.g., a quadrilateral where only one diagonal is valid).
   - Tests that assert no extra adjacency for the central grid vertex beyond `{1,3,5,7}`.

3. **Run full test suite (including refinement and DCEL diagnostics):**
   - Ensure no regressions in refinement, Voronoi, interpolation, or DCEL validation tests.

**Exit criteria:** All topology tests pass; no new failures appear elsewhere.

---

## 6. Rollout and Acceptance

This RFC is considered **complete** when:

1. The adjacency/topology tests for 3×3 grid and rectangle-with-center all pass.  
2. There are no known mismatches between C# Spade and Rust spade / NTS for the explicitly tested configurations.  
3. All previous green tests (including refinement and natural neighbor tests) remain green.  
4. Any remaining minor differences (if unavoidable) are documented in RFCs or comments in the tests.

Once complete, RFC-014, together with RFC-013 and RFC-012, will bring the Delaunay triangulation topology and behaviour of the C# port in line with the Rust reference for the scenarios we care about.

