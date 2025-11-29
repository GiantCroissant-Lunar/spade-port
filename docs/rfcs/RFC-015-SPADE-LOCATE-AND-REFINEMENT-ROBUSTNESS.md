# RFC-015: Spade .NET Locate & Refinement Robustness

**Status:** Completed
**Phase:** Post-DCEL / topology alignment
**Scope:** `spade-port/dotnet/src/Spade/TriangulationBase.cs`, `MeshRefinementExtensions.cs`, and related tests
**Complexity:** ⭐⭐⭐⭐ Hard
**Dependencies:** RFC-012, RFC-013, RFC-014

---

## 1. Motivation

After RFC-013 and RFC-014, most topology and refinement tests are green. The remaining failures highlight robustness issues in **point location** and how it interacts with **refinement**, especially for angle-only refinement:

- `Refine_WithAngleLimit_RefinesEvenWithoutAreaConstraint`
  - Fails with:
    - `BruteForceLocate failed to classify point (-4.6e15, -2.19e17). NumVertices=23, NumFaces=41, NumDirectedEdges=124`
  - This indicates that:
    - Refinement sometimes proposes insertion points with extremely large coordinates (likely due to degeneracies or numerical blow-ups), and
    - `BruteForceLocate` currently treats “no containing face” as a fatal error instead of a handled outcome.

Separately, `LocateWithHintFixedCore` and `BruteForceLocate` are now used more aggressively (e.g., as correctness fallbacks), so their robustness characteristics directly affect refinement and other higher-level features.

We need to:

- Make `BruteForceLocate` and `LocateWithHintFixedCore` robust in the face of numerical edge cases and out-of-hull points, and
- Ensure refinement either avoids or safely handles pathological insertion positions.

---

## 2. Goals

By the end of this RFC:

- **G1: Robust BruteForceLocate.**
  - `BruteForceLocate` never throws for valid triangulations; instead, it:
    - Returns a sensible `PositionInTriangulation` (`OutsideOfConvexHull` or `NoTriangulation`) when the point cannot be assigned to any inner face.
  - The failure seen in `Refine_WithAngleLimit_RefinesEvenWithoutAreaConstraint` is eliminated.

- **G2: Stable refinement with angle-only constraints.**
  - `Refine_WithAngleLimit_RefinesEvenWithoutAreaConstraint` passes without exceptions.
  - Refinement does not generate wildly out-of-scale coordinates (or, if it does, they are treated in a controlled way).

- **G3: No regressions in other tests.**
  - Existing refinement, topology, and interpolation tests remain green (except for any explicitly deferred topology items).

---

## 3. Non-Goals

- Replacing the overall structure of `LocateWithHintFixedCore` with a different algorithm (we still aim to mirror Rust’s walking strategy).
- Implementing new refinement algorithms beyond the existing area/angle-based scheme.
- Guaranteeing robust behaviour for arbitrarily adversarial or unbounded coordinate ranges; we focus on the ranges encountered in FMG and the current test suite.

---

## 4. Design Overview

Two interacting issues must be addressed:

1. **BruteForceLocate behaviour on difficult inputs.**
   - Currently:
     - Iterates over faces, uses `Orient2D` to classify the target as in/out/on-edge.
     - If no face is found, it throws with a detailed message.
   - Desired:
     - When no containing face exists (e.g., point is far outside hull, or the triangulation is degenerate), return a non-throwing `PositionInTriangulation` result.

2. **Refinement’s use of circumcenters/angles.**
   - Angle-only refinement (no area constraint) can compute circumcenters or candidate points that are numerically problematic (e.g., near-collinear inputs or extremely obtuse triangles).
   - We need to:
     - Guard against obviously invalid candidate positions before inserting.
     - Possibly limit refinement in degenerate regions instead of chasing impossible angle improvements.

---

## 5. Detailed Work Items

### 5.1. Harden `BruteForceLocate`

**Goal:** Make `BruteForceLocate` a safe diagnostic tool rather than a source of exceptions.

**Tasks:**

1. **Change failure semantics:**
   - Instead of throwing when no face is found, return a `PositionInTriangulation` such as:
     - `OutsideOfConvexHull` if the point is clearly outside the convex hull, or
     - `NoTriangulation` if we cannot classify reliably.
   - Optionally log a diagnostic line under `SPADE_DIAG_LOCATE` when this happens, but do not throw.

2. **Add a bounding/validity check:**
   - If the target coordinate magnitude is *enormous* compared to the current vertex coordinates (e.g., by comparing against the bounding box of all vertices), treat the point as “outside” without scanning faces.
   - This guards against the `(-4.6e15, -2.19e17)` type values observed in the failing test.

3. **Extend tests:**
   - Add a targeted test that:
     - Constructs a triangulation and calls `BruteForceLocate` with points:
       - Inside a face.
       - On edges/vertices.
       - Clearly outside the hull.
       - With extreme coordinates.
     - Asserts that `BruteForceLocate` returns a non-throwing `PositionInTriangulation` in all cases.

**Exit criteria:** `BruteForceLocate` never throws in the test suite; instead, it returns a well-defined enum value and optional diagnostics.

---

### 5.2. Align `LocateWithHintFixedCore` with hardened BruteForceLocate

**Goal:** Ensure `LocateWithHintFixedCore` uses `BruteForceLocate`’s new semantics correctly.

**Tasks:**

1. **Review all call sites where `BruteForceLocate` is currently used as a fallback:**
   - In `LocateWithHintFixedCore`, we already have places where:
     - After an “outer face” detection, we call `BruteForceLocate` and then either:
       - Use its `OnFace` result, or
       - Fall back to `OutsideOfConvexHull`.
   - Update these branches to handle the new non-throwing return values:
     - If `BruteForceLocate` yields `OnFace`, use it.
     - If it yields `OnEdge`/`OnVertex`, follow that classification.
     - If it yields `OutsideOfConvexHull`/`NoTriangulation`, proceed with the existing “outside hull” logic.

2. **Add comparison tests (if not already covered by RFC-012):**
   - For a few triangulations (including one refined mesh), compare:
     - `LocateWithHintFixedCore` vs `BruteForceLocate` on a handful of points (e.g., those used during refinement).
   - Assert they agree on classification in non-degenerate cases and never throw.

**Exit criteria:** `LocateWithHintFixedCore` and `BruteForceLocate` work together without exceptions; the angle-refinement test no longer triggers a thrown locate error.

---

### 5.3. Guard refinement candidate positions

**Goal:** Prevent refinement from chasing numerically crazy positions.

**Tasks:**

1. **Instrument refinement to log problematic positions:**
   - Under `SPADE_DIAG_REFINE`, log:
     - Candidate circumcenters/barycenters used for angle-only refinement.
     - Any candidate point that leads to a failed `LocateWithHintOptionCore` or fallback path.

2. **Add sanity checks on candidate points before insertion:**
   - Compute a bounding box for current vertices.
   - If a candidate point lies far outside a reasonable expansion of that bounding box (e.g., more than N times the hull diameter), treat it as invalid:
     - Either skip that refinement step, or
     - Mark the triangle as “refinement-complete” to avoid infinite retries.

3. **Update `Refine_WithAngleLimit_RefinesEvenWithoutAreaConstraint`:**
   - Ensure the test remains meaningful:
     - It should still verify that some refinement happens on a skinny triangle.
     - But it should not require refinement in cases where the numeric geometry is degenerate or unsalvageable.

**Exit criteria:** Refinement no longer generates extreme coordinates that cause locate failures; `Refine_WithAngleLimit_RefinesEvenWithoutAreaConstraint` passes.

---

### 5.4. Regression and stress checks

**Goal:** Ensure no new fragility is introduced.

**Tasks:**

1. **Re-run the full test suite:**
   - Confirm no new failures in:
     - Other refinement tests.
     - Voronoi/topology tests.
     - Interpolation/natural neighbor tests.

2. **Add a small stress test (optional):**
   - Angle-only refinement on a modest random point set, verifying no exceptions and reasonable runtime.

**Exit criteria:** All existing green tests remain green; no new exceptions arise under typical FMG-style inputs.

---

## 6. Acceptance Criteria

This RFC is considered **complete** when:

1. `Refine_WithAngleLimit_RefinesEvenWithoutAreaConstraint` passes without exceptions.
2. `BruteForceLocate` and `LocateWithHintFixedCore` no longer throw for any tests in `Spade.Tests`.
3. No new failures appear in the existing refinement, topology, or interpolation tests.
4. Any remaining known limitations (if any) are documented in comments or related RFCs.

---
