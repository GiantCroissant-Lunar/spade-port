# FMG-Spade Refinement Handover

## Scope

This note captures the current state of the C# port of spade's **CDT refinement** pipeline and the remaining work needed to align it with the Rust implementation, especially around DCEL face topology and inner/outer faces.

The focus is on:

- `ConstrainedDelaunayTriangulation` (CDT, constraints + DCEL)
- Mesh refinement (`MeshRefinementExtensions.Refine`)
- DCEL face semantics and how they affect refinement tests

## Current Status

### CDT / Constraints

Key files:

- `dotnet/src/Spade/ConstrainedDelaunayTriangulation.cs`
- `dotnet/src/Spade/DCEL/DcelOperations.cs`
- `dotnet/src/Spade/DCEL/Dcel.cs`
- `dotnet/tests/Spade.Tests/ConstrainedDelaunayTriangulationTests.cs`

What's working:

- Constraint insertion logic (`AddConstraint`, `ResolveSplittingConstraintRequest`, `TryAddConstraintInternal`, `ResolveConflictRegion`) has been brought much closer to the Rust `cdt.rs` implementation.
- The polygon-with-hole test now passes:
  - `AddConstraints_PolygonWithHole_BoundaryConstraintsAndPreventsCrossing`
  - All other CDT tests under `ConstrainedDelaunayTriangulationTests` and `ConstrainedDelaunayConstraintSplittingTests` also pass.
- For rare cases where the line-intersection iterator in the C# port misses the final `VertexIntersection` at the target vertex, `GetConflictResolutions` has a fallback that attaches the remaining conflict edges to the target vertex, mirroring spade's behavior.
- `AddConstraint` includes a safe **fallback path**:
  - If `TryAddConstraintInternal(from,to)` produced no constraint edges but `CanAddConstraint(from,to)` is true, it finds an existing undirected edge path between `from` and `to` (`FindExistingEdgePath`) and marks those edges as constrained.
  - This only uses existing DCEL edges and keeps constraints non-crossing.

Diagnostics:

- `SPADE_DIAG_POLYHOLE`:
  - When set to `1` during CDT tests, `ConstrainedDelaunayTriangulation.GetConflictResolutions` writes `polyhole_diag.txt` (in the test bin directory) logging:
    - The new constraint endpoints
    - Each `Intersection` (edge / vertex / overlap)
    - The derived conflict groups
- `ConstrainedDelaunayTriangulationTests.AddConstraints_PolygonWithHole_BoundaryConstraintsAndPreventsCrossing` also writes a `polyhole_diag.txt` with vertices and constraint edges when `SPADE_DIAG_POLYHOLE=1`.

### Mesh Refinement

Key files:

- `dotnet/src/Spade/Refinement/MeshRefinementExtensions.cs`
- `dotnet/src/Spade/Refinement/RefinementParameters.cs`
- `dotnet/src/Spade/Refinement/RefinementResult.cs`
- `dotnet/tests/Spade.Tests/Refinement/MeshRefinementTests.cs`

What's implemented:

- `Refine` performs iterative refinement over `InnerFaces()`:
  - Uses area and (optionally) angle limits from `RefinementParameters` to compute a `RefinementHint` per face (Ignore / ShouldRefine / MustRefine).
  - Picks the face with the highest combined area/angle score as the "worst" face in each iteration.
  - Insert location:
    - With **angle refinement enabled**: use face circumcenter (`FaceHandle.Circumcenter()`).
    - With **area-only refinement**: use barycenter (`ComputeTriangleBarycenter`) for more robust splitting of large faces.
  - Respects `MaxAdditionalVertices` to cap refinement.
- Encroachment handling for constraints:
  - `TryGetEncroachedConstraintEdge` scans constrained undirected edges and tests whether a candidate point lies in the edge's diametral circle (`IsEncroachingEdge`), matching spade's semantics.
  - When `KeepConstraintEdges` is true and we detect encroachment, we call `ResolveEncroachment` instead of inserting the circumcenter/barycenter directly.
- `ResolveEncroachment`:
  - Signature matches what the tests expect (4 parameters, including the two queues), so reflection in `MeshRefinementTests.ResolveEncroachment_SplitsConstraintEdgeAtMidpoint` can locate and call it.
  - Implementation: splits an encroached edge at its (mitigated) midpoint and inserts that vertex via `triangulation.Insert(mid)`.
  - This is simpler than Rust's full "nearest power of two" logic, but good enough for the current tests around a single midpoint.
- `RefinementResult` is wired up: `AddedVertices` and `ReachedVertexLimit` are returned by `Refine`.

Diagnostics:

- `SPADE_DIAG_REFINE`:
  - When set to `1`, `Refine` appends `refine_diag.txt` in the test bin directory with per-iteration snapshots:
    - `added`, `bestScore`, `maxArea` and `numFaces`
    - For the first iteration or two, each inner face's index, area, and vertex positions.

### Triangulation / InnerFaces

Key file:

- `dotnet/src/Spade/TriangulationBase.cs`

Changes:

- `InnerFaces()` no longer blindly returns all faces with index `> 0`:
  - For each face:
    - Skips if it is marked as outer (`face.IsOuter`).
    - Walks the face's edge ring starting from `AdjacentEdge`, counting edges up to a safety bound.
    - Only yields the face if the ring is a simple triangle (3 distinct edges, no early cycles, no excessive steps).
  - This is a defensive filter to prevent refinement from processing malformed or non-triangular faces.

## Failing Refinement Tests (Current)

These still fail and are the drivers for the deeper DCEL work:

- `Refine_WithMaxArea_SplitsLargeTriangle`
  - Setup: a single large triangle with vertices `(0,0)`, `(10,0)`, `(0,10)`; initial area 50.
  - Expectation: after refinement with `MaxAllowedArea=5.0`, the worst inner triangle area should be **less** than the initial 50.
  - Observed (via `refine_diag.txt`):
    - After inserting a Steiner point, the DCEL has:
      - Two small triangles (~16.67 area) involving the new point.
      - But also a **50-area triangle using only the original hull vertices**, which remains classified as an inner face.
    - As a result, `refinedMaxArea` computed over `cdt.InnerFaces()` never drops below 50.0, and the test fails.

- `Refine_WithConstraintEncroachment_SplitsConstraintAndStopsRefining`
  - The first refinement step splits the constraint edge as expected and inserts the midpoint, but:
    - The refinement loop continues to add vertices, trying to reduce an (incorrectly) large inner face area.
    - The test expects `result.AddedVertices == 0`, but we currently see `AddedVertices == 10`.
  - This is essentially the same underlying issue: **outer faces are mis-identified as inner**, so refinement keeps chasing them.

## Root Cause: Missing Face Tags (Inner vs Outer)

In Rust spade:

- Faces are parameterized with `InnerTag`/`OuterTag` and various helpers (`fixed_inner_faces`, `calculate_outer_faces`) distinguish inner faces from triangulated outer/exterior regions.
- `insert_into_triangle` and related DCEL operations adjust these tags via `adjust_inner_outer()` so faces created from splitting a hull triangle can be correctly classified as outer faces, even if they are finite.

In the C# port:

- `FaceEntry<F>` has no direct tag; `FaceHandle.IsOuter` is simply `handle.Index == 0`.
- `TriangulationBase.InnerFaces()` previously treated every face with `Index > 0` as inner.
- DCEL operations (`InsertIntoTriangle`, `SplitEdge`, `CreateNewFaceAdjacentToEdge`, `CreateSingleFaceBetweenEdgeAndNext`) do not tag or distinguish inner vs outer faces beyond index `0`.

Net effect:

- When we insert a point into the initial large triangle:
  - New small inner faces are created correctly.
  - But the **original hull triangle** is not clearly distinguished as outer in the DCEL and remains in the "inner" face set.
  - Both refinement and tests see that 50-area face as an inner face, causing the max-area condition to fail.

## Progress This Session (C# Port)

This session focused on getting the C# port structurally closer to spade and setting up the next phase of work:

- **Face tagging infrastructure**
  - `FaceEntry<F>` in `DcelEntries.cs` now carries a `FaceKind` tag (`Inner` / `Outer`).
  - `Dcel<V,DE,UE,F>` initializes face index `0` as `FaceKind.Outer`; all newly created faces default to `FaceKind.Inner`.
  - `TriangulationBase.InnerFaces()` now uses `FaceKind` (via `_dcel.Faces[i].Kind`) to decide which finite faces are candidates, on top of the existing 3-edge triangle ring check.
- **Outer-face classification for refinement**
  - `MeshRefinementExtensions.Refine` now calls `MarkOuterFacesUsingRefinementTopology` at the end of refinement.
  - `MarkOuterFacesUsingRefinementTopology` uses a C# port of spade's `calculate_outer_faces` to compute which *finite* faces lie in the exterior region and tags those faces as `FaceKind.Outer`.
  - This is implemented via:
    - `CalculateOuterFaceIndices` (port of `calculate_outer_faces`),
    - `GetConvexHullEdges` (approximate `convex_hull` based on edges adjacent to the structural outer face),
    - `IsConstraintEdge` (checks `CdtEdge<UE>.IsConstraintEdge`).
- **Test status after these changes**
  - The two key refinement tests are still failing:
    - `Refine_WithMaxArea_SplitsLargeTriangle` still sees a 50-area hull triangle in the inner-face set after refinement.
    - `Refine_WithConstraintEncroachment_SplitsConstraintAndStopsRefining` still reports `AddedVertices == 10` (refinement keeps chasing the outer triangle).
  - Diagnostics (`refine_diag.txt`) confirm that the DCEL still contains the original 50-area hull triangle after the first Steiner insertion; the issue appears to be in the DCEL split / hull-update logic, not in the tagging or outer-face classification layer.

## Recommended Next Steps

To bring refinement fully in line with Rust spade, we should realign the DCEL and face tagging. Use this as a concrete, step‑by‑step implementation checklist:

1. **Introduce face tags (inner vs outer) in the DCEL**
   - [ ] Add a `FaceKind` enum (e.g. `Inner`, `Outer`) in `dotnet/src/Spade/DCEL/Dcel.cs`.
   - [ ] Add a `Kind` (or `FaceKind`) field/property to `FaceEntry<F>` and include it in all constructors / factory helpers.
   - [ ] Update `FaceHandle.IsOuter` in `dotnet/src/Spade/Handles/DynamicHandles.cs` to read from the tag instead of `Index == 0` (keep `Index == 0` as the canonical outer face).
   - [ ] Ensure all existing faces created in `Dcel` (including the initial outer face) are initialized with a non-default, explicit tag.

2. **Wire up initial tagging when building the triangulation**
   - [ ] Locate the C# equivalent of Rust's `new_with_fixed_vertices` (likely in `DcelOperations` / triangulation construction path).
   - [ ] Tag the structural outer face as `FaceKind.Outer` and any initial hull / exterior faces as `Outer` as well.
   - [ ] Tag all interior faces created during initialization as `FaceKind.Inner`.
   - [ ] Add assertions (in debug builds) that exactly one distinguished outer face handle exists and that it is tagged `Outer`.

3. **Port critical DCEL operations with tag updates**
   - [ ] Port Rust's `insert_into_triangle` logic into the corresponding C# method, preserving when and how faces are split.
   - [ ] For `insert_into_triangle`, explicitly assign `FaceKind` for the updated original face and the newly created faces (`f0`, `f1`, `f2`), matching Rust's behavior.
   - [ ] Port or mirror Rust's `adjust_inner_outer()` logic so that when a hull triangle is split, you decide which resulting faces remain `Outer` and which become `Inner`.
   - [ ] Audit and update the following methods so they always maintain consistent tags when they create or split faces:
     - [ ] `SplitHalfEdge`
     - [ ] `SplitEdge`
     - [ ] `CreateNewFaceAdjacentToEdge`
     - [ ] `CreateSingleFaceBetweenEdgeAndNext`
   - [ ] After changes, add targeted assertions (e.g. helper that scans all faces) to ensure no face has an uninitialized or contradictory tag.

4. **Reimplement `InnerFaces()` on top of tags**
   - [ ] Change `TriangulationBase.InnerFaces()` in `dotnet/src/Spade/TriangulationBase.cs` to yield faces whose tag is `Inner` (or `!IsOuter`), not just `Index > 0`.
   - [ ] Keep the existing defensive triangle-shape checks (simple 3-edge ring) and apply them after filtering by tag.
   - [ ] Optionally add a helper analogous to Rust's `calculate_outer_faces` if needed (e.g. an `EnumerateOuterFaces()` or `ExcludeOuterFaces()` helper) and base it on the tag rather than topology alone.

5. **Re-run refinement tests and iterate**
   - [ ] Run `Refine_WithMaxArea_SplitsLargeTriangle` and confirm that no 50-area face remains in the inner set; the worst inner area should drop below the original 50.
   - [ ] Run `Refine_WithConstraintEncroachment_SplitsConstraintAndStopsRefining` and confirm that constraint-encroachment refinement splits edges once, then stabilizes (area refinement no longer chases the outer triangle; `AddedVertices` stops increasing for that phase).
   - [ ] If either test still fails, log all `InnerFaces()` with their tags and areas into `refine_diag.txt` and compare against Rust's `fixed_inner_faces` / `calculate_outer_faces` behavior.

6. **Keep diagnostics while aligning behavior**
   - [ ] Keep `refine_diag.txt` (and `SPADE_DIAG_REFINE`) enabled while aligning DCEL behavior; they are valuable for inspecting real faces and areas.
   - [ ] Once tests are green and DCEL tagging is stable, decide whether to keep the diagnostics always-on, behind env flags only, or to trim them down.

## Quick Pointers for the Next Engineer

- Start from the Rust sources:
  - `ref-projects/spade/src/delaunay_core/dcel.rs`
  - `ref-projects/spade/src/delaunay_core/dcel_operations.rs` (`insert_into_triangle`, `new_with_fixed_vertices`, etc.)
  - `ref-projects/spade/src/delaunay_core/refinement.rs` for how inner faces and excluded faces are computed.
- Keep changes centered in:
  - `dotnet/src/Spade/DCEL/Dcel.cs`
  - `dotnet/src/Spade/DCEL/DcelOperations.cs`
  - `dotnet/src/Spade/Handles/DynamicHandles.cs` (face API)
  - `dotnet/src/Spade/TriangulationBase.cs` (`InnerFaces()`)
  - `dotnet/src/Spade/Refinement/MeshRefinementExtensions.cs`
- Use `MeshRefinementTests` as your primary oracle for when refinement behavior matches spade's expectations.
