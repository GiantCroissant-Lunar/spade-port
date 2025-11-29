# RFC-013: Spade .NET DCEL & Hull Topology Realignment

**Status:** Draft
**Phase:** Targeted DCEL / refinement alignment (post RFC-012)
**Scope:** `spade-port/dotnet/src/Spade` DCEL + hull + refinement, plus related tests
**Complexity:** ⭐⭐⭐⭐ Hard
**Dependencies:** RFC-004 (Mesh Refinement), RFC-012 (Port Completion)

---

## 1. Motivation

The current C# Spade port passes most tests but still diverges from the Rust reference in several critical areas:

- Refinement tests:
  - `Refine_WithMaxArea_SplitsLargeTriangle` still sees the initial 50‑area hull triangle as an inner face after refinement.
  - `Refine_WithConstraintEncroachment_SplitsConstraintAndStopsRefining` keeps refining instead of stabilizing after splitting the encroached constraint.
- Topology/oracle tests:
  - `VoronoiTests.DelaunayTopology_Matches_OriginalSpade_On_3x3_Grid`
  - `VoronoiTests.DelaunayTopology_SpadeVsNts_On_RectangleWithCenter`
  - `DelaunayOracleTests.DelaunayTopology_SpadeVsNts_On_RectangleWithCenter`
  report adjacency mismatches vs Rust spade / NTS.
- Natural neighbor interpolation:
  - `GradientInterpolationTests` previously threw `InvalidOperationException("Encountered outer face while traversing natural neighbor polygon")` during polygon walks.

Diagnostics and hand‑analysis indicate that these issues are not primarily caused by refinement policy, but by **DCEL topology and hull update semantics**:

- Finite outer faces (those forming the exterior region with respect to constraints) are not always tagged/excluded correctly.
- Hull insertions and edge splits differ subtly from the Rust implementation, leaving “forgotten” outer triangles tagged as inner.
- Natural neighbor polygon traversal sometimes crosses into outer faces where Rust stays strictly in the interior region.

The goal of this RFC is to realign the DCEL, hull topology, and refinement integration so that:

- C# triangulations have the same inner/outer face semantics as Rust spade.
- Refinement and topology tests match Rust’s expectations.
- Natural neighbor operations no longer encounter outer faces in valid configurations.

---

## 2. Goals

By the end of this RFC:

- **G1: DCEL invariants match Rust spade for the features we use.**
  - Structural outer face is always `Faces[0]` and tagged as outer.
  - Half‑edge rings around every face are consistent (prev/next/face/origin) after any operation.
  - Hull triangles and finite outer faces are consistently represented and tagged.

- **G2: Insertion pipeline parity.**
  - `InsertWithHintOptionImpl` and its sub‑paths (`InsertIntoFace`, `InsertOnEdge`, `InsertOutsideOfConvexHull`) behave like Rust’s `insert_with_hint_option_impl` and `insert_outside_of_convex_hull` in `triangulation_ext.rs`.

- **G3: DCEL operations parity, especially on hull and splits.**
  - `InsertIntoTriangle`, `SplitHalfEdge`, `SplitEdge`, `CreateNewFaceAdjacentToEdge`, `CreateSingleFaceBetweenEdgeAndNext`, and `FlipCw` match the Rust algorithms in `dcel_operations.rs`.

- **G4: Inner/outer classification parity for refinement.**
  - A direct C# port of Rust’s `calculate_outer_faces` drives face tagging.
  - `InnerFaces()` and refinement see exactly the same inner face set as Rust (`fixed_inner_faces` / `calculate_outer_faces`).

- **G5: Test and behavior alignment.**
  - All of the following pass without workarounds:
    - `Refine_WithMaxArea_SplitsLargeTriangle`
    - `Refine_WithConstraintEncroachment_SplitsConstraintAndStopsRefining`
    - `VoronoiTests.DelaunayTopology_Matches_OriginalSpade_On_3x3_Grid`
    - `VoronoiTests.DelaunayTopology_SpadeVsNts_On_RectangleWithCenter`
    - `DelaunayOracleTests.DelaunayTopology_SpadeVsNts_On_RectangleWithCenter`
  - Natural neighbor gradient tests (`GradientInterpolationTests`) do not encounter outer faces during polygon traversal.

---

## 3. Non‑Goals

- Changing the public Spade .NET API shape (types and method signatures remain compatible).
- Implementing new features beyond the Rust spade surface area we are already targeting.
- Replacing higher‑level FMG logic or adding new map‑generation algorithms.
- Micro‑optimizing performance beyond what the clean DCEL alignment naturally yields.

---

## 4. Design Overview

### 4.1. Face tagging and invariants

The C# port uses `FaceKind` as a lightweight analogue of Rust’s `InnerTag` / `PossiblyOuterTag` system:

- `FaceKind.Inner` represents a finite face that belongs to the triangulation interior.
- `FaceKind.Outer` represents the structural outer face (`Faces[0]`) and any finite outer faces that lie in the exterior region w.r.t. constraints.

Invariants (enforced in DEBUG builds):

- `Faces.Count >= 1`, and `Faces[0].Kind == FaceKind.Outer`.
- There are no faces with an uninitialized or “unknown” kind.
- Any face whose `Kind == FaceKind.Outer` is treated as excluded by `InnerFaces()` and refinement.

`FaceHandle.IsOuter` delegates to the tag instead of `Index == 0`, while preserving index 0 as the canonical structural outer face.

### 4.2. Insertion pipeline

We realign `TriangulationBase.InsertWithHintOptionImpl` with Rust’s `TriangulationExt::insert_with_hint_option_impl`:

- **Case 0:** `NumVertices == 0` → append unconnected vertex via `AppendUnconnectedVertex`.
- **Case 1:** `NumVertices == 1` → either update the existing vertex or set up the initial two vertices via `SetupInitialTwoVertices` (outer face only).
- **Line case:** `AllVerticesOnLine()` → `LocateWhenAllVerticesOnLine` + `InsertWhenAllVerticesOnLine` (already ported conceptually).
- **General case:**
  - Use `LocateWithHintOptionCore` to obtain a `PositionInTriangulation`:
    - `OnFace` → `InsertIntoFace` → `InsertIntoTriangle`.
    - `OnEdge` → `InsertOnEdge` → `SplitHalfEdge` / `SplitEdge` / `SplitEdgeWhenAllVerticesOnLine`.
    - `OutsideOfConvexHull` → `InsertOutsideOfConvexHull` (see next subsection).
    - `OnVertex` → update existing vertex data.
  - After insertion, fully legalize interior edges (mirroring Rust’s `fully_legalize_all_interior_edges`).

### 4.3. Hull insertion: `InsertOutsideOfConvexHull`

`InsertOutsideOfConvexHull` is the main entry point for adding vertices outside the current convex hull. The design follows Rust’s `insert_outside_of_convex_hull`:

- Start from a hull edge `convexHullEdge` (validated via `FindHullEdgeOrThrow` to ensure it is a true hull edge).
- Use `CreateNewFaceAdjacentToEdge` to attach a face that includes the new vertex and the chosen hull edge.
- Walk CCW and CW along the hull:
  - While the new vertex lies to the left of an adjacent hull edge, call `CreateSingleFaceBetweenEdgeAndNext` to “peel” that hull segment and incorporate it into the new outer layer.
  - Legalize local edges after each hull modification using `LegalizeEdge`, similar to Rust’s combination of local flips and hull updates.
- Maintain the invariant that hull edges always have one outer face and one interior face, and that the outer ring remains simple and well‑formed.

### 4.4. DCEL operations

We treat the following DCEL operations as **must‑match** ports from Rust (`dcel_operations.rs`):

- `InsertIntoTriangle` – splitting a triangle face into three around a new vertex, including correct reassignment of face handles for new and existing half‑edges.
- `SplitHalfEdge` – splitting an edge that lies on the hull, producing a new face and updating adjacency for both sides of the edge.
- `SplitEdge` – splitting an interior edge, producing two new faces in addition to updating the original faces.
- `CreateNewFaceAdjacentToEdge` – used when inserting outside the convex hull to create a new finite face adjoining a hull edge and new vertex.
- `CreateSingleFaceBetweenEdgeAndNext` – used to “peel” hull layers by inserting a new edge between `edge.next().to()` and `edge.from()` and creating a finite face in between.
- `FlipCw` – flipping an interior edge, including updating `Face.AdjacentEdge` for both incident faces and maintaining vertex `OutEdge` pointers.

Each of these operations is responsible for:

- Maintaining consistent half‑edge rings (`Next`/`Prev`/`Face`/`Origin`).
- Updating `VertexEntry.OutEdge` and `FaceEntry.AdjacentEdge` for affected vertices/faces.
- Respecting `FaceKind` semantics:
  - Structural outer face remains `Outer`.
  - Finite outer faces either remain outer or are converted to inner according to the refinement/hull layer semantics (see below).

### 4.5. Outer face computation and tagging

We align `MeshRefinementExtensions.CalculateOuterFaceIndices` with Rust’s `calculate_outer_faces` in `refinement.rs`:

- Start from the convex hull edges, oriented so that the interior lies on the left; then take each edge’s reverse as the starting “layer” (mirroring `.convex_hull().map(|edge| edge.rev())`).
- Alternate between two sets, `inner_faces` and `outer_faces`, while “peeling” layers:
  - If crossing a constraint edge (or fixed edge), move to the next layer (`inner_faces`).
  - Otherwise, remain in the current outer layer (`outer_faces`).
  - Only add faces that are not the structural outer face and not already visited.
- After draining the outermost layer, swap `inner_faces` and `outer_faces` and continue to peel until no further layers remain.
- The resulting `HashSet<int>` of outer faces is used to tag `FaceKind.Outer` for finite faces; all other non‑structural faces are tagged `FaceKind.Inner`.

`TriangulationBase.InnerFaces()` is then defined as:

- All faces with `Kind == FaceKind.Inner`, whose face ring is a simple triangle (3 distinct edges, consistent `Next`/`Prev`), excluding any malformed or degenerate faces.

### 4.6. Natural neighbor interpolation

With correct hull/outer face semantics, natural neighbor interpolation should no longer encounter outer faces when walking the natural neighbor polygon for interior queries. As a safety net:

- `NaturalNeighborInterpolator.Interpolate` and `InterpolateGradient` catch `InvalidOperationException` from `GetWeights` and fall back to barycentric interpolation.

This fallback remains, but the expectation after DCEL realignment is that the exception path becomes extremely rare or unused in valid inputs.

---

## 5. Detailed Work Items

### 5.1. Reconfirm and enforce DCEL invariants

1. **Face tags & constructor**
   - Ensure `Dcel` constructor always creates exactly one structural outer face (`index 0`, `Kind = FaceKind.Outer`), with `AdjacentEdge = null`.
   - Add a DEBUG‑only `AssertOuterFaceInvariant` that checks `Faces.Count > 0` and `Faces[0].Kind == FaceKind.Outer`.

2. **Half‑edge accessors**
   - Audit `GetHalfEdge` and `UpdateHalfEdge` overloads to guarantee:
     - No method partially updates a `HalfEdgeEntry` without writing it back.
     - All face/vertex indices referenced by `HalfEdgeEntry` are in range.

3. **DEBUG validation**
   - Keep and possibly extend `ValidateFaceRings()` in `Dcel` to verify face rings after hull and split operations, not just in refinement tests.

### 5.2. Port `InsertIntoTriangle` from Rust

1. Compare `DcelOperations.InsertIntoTriangle` against Rust’s `insert_into_triangle`:
   - Confirm edge indices (`e0`..`e8`), vertex handles (`v0`..`v2`), and face handles (`f0`..`f2`) follow the same pattern.
   - Adjust any mismatches in `Face` assignments for new and existing half‑edges.
   - Ensure `FaceKind` updates are correct:
     - The original face `f0` should remain inner/outer according to Rust’s `f0.adjust_inner_outer()` semantics (mirrored via `FaceKind`).

2. Add targeted diagnostics (conditionally compiled or env‑guarded) to log face rings before/after `InsertIntoTriangle` for debugging.

### 5.3. Port hull‑related DCEL operations

1. **`CreateNewFaceAdjacentToEdge`**
   - Align with Rust’s `create_new_face_adjacent_to_edge`:
     - The new face’s adjacency (`AdjacentEdge`) should match the new inner edge.
     - Half‑edge `Face`, `Next`, `Prev`, and `Origin` should align with Rust’s diagrammed topology.
   - Tag the new face as `FaceKind.Inner` by default; allow refinement to retag finite outer faces later.

2. **`CreateSingleFaceBetweenEdgeAndNext`**
   - Align with Rust’s `create_single_face_between_edge_and_next`:
     - One side (the new inner face) receives a new face handle.
     - The other side remains or becomes outer (face 0 or a finite outer face).
   - Ensure `FaceKind` for the new face is `Inner`, and that the outer side references the structural outer face handle or an appropriately tagged outer face.

3. **`SplitHalfEdge`, `SplitEdge`, `SplitEdgeWhenAllVerticesOnLine`**
   - Verify the mapping of edge/face/vertex handles (`e0`..`e3`, `t0`..`t3`, `f0`..`f3`) against Rust.
   - Confirm that hull edges maintain one outer face and one interior face as in Rust.

4. **`FlipCw`**
   - Ensure `FlipCw` updates:
     - `HalfEdgeEntry` for both directions of the flipped edge and their neighbors.
     - `VertexEntry.OutEdge` for affected vertices.
     - `FaceEntry.AdjacentEdge` for both incident faces.

### 5.4. Realign `InsertOutsideOfConvexHull`

1. Inspect Rust’s `insert_outside_of_convex_hull` in `triangulation_ext.rs` and port the logic:
   - Starting from the chosen hull edge, create a new face with the new vertex and that edge.
   - Walk CCW and CW along the hull, creating single faces between edges and their neighbors while the query lies on the “visible” side.
   - Use local edge flips where required to maintain Delaunay properties.

2. Verify that:
   - The convex hull remains a simple loop of edges, with outer faces on the outside.
   - No interior faces are incorrectly tagged as outer or left with invalid rings.

### 5.5. Port and integrate `calculate_outer_faces` semantics

1. Compare the current `CalculateOuterFaceIndices` in `MeshRefinementExtensions` with Rust’s `calculate_outer_faces`:
   - Adjust start edge selection to match `convex_hull().map(|edge| edge.rev())`.
   - Mirror the layer‑peeling logic (outer vs inner sets, constraint crossing behavior).

2. Use the resulting set of outer face indices to:
   - Retag `FaceKind` in `MarkOuterFacesUsingRefinementTopology`.
   - Feed any future helper analogous to `fixed_inner_faces()` if needed.

3. Keep `AssertFaceKindsConsistent` in DEBUG to ensure the tag set matches the computed outer set.

### 5.6. Validation and regression tests

1. **Refinement tests**
   - `Refine_WithMaxArea_SplitsLargeTriangle`:
     - Confirm that after refinement, the worst inner triangle area is reduced (no surviving 50‑area hull triangle).
   - `Refine_WithConstraintEncroachment_SplitsConstraintAndStopsRefining`:
     - Confirm that only the encroaching constraint edge is split once, and `AddedVertices == 0` for the refinement phase after encroachment resolution.

2. **Topology/oracle tests**
   - `VoronoiTests.DelaunayTopology_Matches_OriginalSpade_On_3x3_Grid`
   - `VoronoiTests.DelaunayTopology_SpadeVsNts_On_RectangleWithCenter`
   - `DelaunayOracleTests.DelaunayTopology_SpadeVsNts_On_RectangleWithCenter`
   - Ensure adjacency lists match expected ones (Rust spade / NTS), especially for hull and “center” vertices.

3. **Natural neighbor tests**
   - `GradientInterpolationTests`:
     - Verify no `InvalidOperationException` is thrown during natural neighbor polygon construction in expected use cases.
     - Ensure fallback to barycentric remains only as a robustness measure for degenerate inputs.

4. **Optional: DCEL sanity tests**
   - Add internal tests (or test‑only helpers) to:
     - Construct simple hull + interior configurations and verify face tags and rings.
     - Call `ValidateFaceRings` after sequences of operations (insertion, refinement, constraint addition).

---

## 6. Rollout Strategy

- Implement changes behind the existing APIs; no public type or method signatures should change.
- Use the existing test suite as the primary validation harness; do not merge until all relevant tests pass.
- Keep diagnostics (`SPADE_DIAG_REFINE`, DCEL ring logging) available during development; decide later whether to keep them permanently or behind env flags.
- Document any remaining edge cases or known limitations in `FMG-SPADE-REFINEMENT-HANDOVER.md` and/or a short “Differences vs Rust” section in the main README.

---

## 7. Open Questions / Risks

- **Q1:** Are there corner cases where Rust’s inner/outer tagging relies on behavior not directly expressible via `FaceKind`?
  - Mitigation: cross‑check random configurations against Rust’s `fixed_inner_faces` / `calculate_outer_faces` outputs.

- **Q2:** Will strict parity with Rust’s hull insertion logic introduce performance regressions in C#?
  - Mitigation: keep the code idiomatic but structurally similar; only optimize after correctness is established.

- **Q3:** Are there downstream FMG assumptions depending on the current (incorrect) behavior?
  - Mitigation: run FMG integration scenarios after DCEL changes and adjust only if they relied on bugs.

This RFC is considered complete when the DCEL/hull operations are structurally aligned with Rust, all relevant tests are green, and the invariants described here hold in DEBUG builds.
