# RFC-016: Spade .NET Natural Neighbor Robustness

**Status:** Completed
**Phase:** Post-topology alignment / interpolation correctness
**Scope:** `spade-port/dotnet/src/Spade/NaturalNeighborInterpolator.cs` and related tests
**Complexity:** ⭐⭐⭐ Medium–Hard
**Dependencies:** RFC-012, RFC-013, RFC-014, RFC-015

---

## 1. Motivation

After recent DCEL and topology fixes, several **natural neighbor** tests still fail:

- `GradientInterpolationTests.InterpolateGradient_UsingEstimatedGradients_MatchesPlanarField`
  - Fails because `InterpolateGradient` returns `null` for some queries.
- `InterpolationTests.NaturalNeighbor_SlopeField_MatchesExpectedXWithinHull`
  - Also sees `null` for queries that are **inside** the convex hull.

These failures indicate that:

- `NaturalNeighborInterpolator.GetNaturalNeighborEdges` or `GetNaturalNeighborWeights` sometimes returns an empty neighbor set for interior points, and
- The current robustness fallbacks (e.g., to barycentric interpolation) are not sufficient or not triggered in all code paths.

We need to make natural neighbor interpolation:

- Reliable for interior points (never `null` when the query lies clearly inside the hull), and
- Gracefully degraded (fallback) in degenerate or boundary cases.

---

## 2. Goals

By the end of this RFC:

- **G1: No `null` inside the hull.**
  - For queries lying strictly inside the convex hull of the sites:
    - `NaturalNeighborInterpolator.Interpolate` and `InterpolateGradient` should either:
      - Return a finite value, or
      - Fall back to barycentric interpolation, but **not** return `null`.

- **G2: Tests pass.**
  - The following tests pass:
    - `InterpolationTests.NaturalNeighbor_SlopeField_MatchesExpectedXWithinHull`
    - `GradientInterpolationTests.InterpolateGradient_UsingEstimatedGradients_MatchesPlanarField`

- **G3: No regression in other interpolation behaviour.**
  - Existing tests for natural neighbor and barycentric interpolation remain green.

---

## 3. Non-Goals

- Achieving perfect parity with Rust’s natural neighbor implementation for all possible inputs; we focus on:
  - The planar fields and slope fields used in the tests.
  - Typical FMG usage patterns.
- Changing the public API shape of `NaturalNeighborInterpolator`.

---

## 4. Design Overview

Natural neighbor interpolation proceeds in two steps:

1. **Neighbor edge discovery** (`GetNaturalNeighborEdges`).
2. **Weight computation** (`GetNaturalNeighborWeights`), which constructs an “insertion cell” polygon and assigns areas to neighbours.

The failure pattern suggests one or both of the following:

- For some interior queries, `GetNaturalNeighborEdges` returns too few edges (0 or 1) even though the point is inside the hull.
- For some interior queries and/or edge sets, `GetNaturalNeighborWeights` produces a total area of zero or an empty result.

We will:

- Improve diagnostics and detection of these cases.
- Strengthen the logic that ensures a reasonable set of neighbours inside the hull.
- Use robust fallbacks (barycentric interpolation) when natural neighbor construction is not reliable.

---

## 5. Detailed Work Items

### 5.1. Instrument and diagnose empty-neighbor cases

**Goal:** Understand exactly when and why we get empty weights inside the hull.

**Tasks:**

1. **Add diagnostics under `SPADE_DIAG_NN`:**
   - In `GetNaturalNeighborEdges`, when `neighborEdges.Count == 0` or `== 1` for a query, log:
     - Query position.
     - Convex hull bounding box (min/max of vertex positions).
     - Whether the query lies inside that box.
   - In `GetNaturalNeighborWeights`, when `neighborEdges.Count >= 3` but `totalArea == 0.0` or `result.Count == 0`, log:
     - Query position.
     - All neighbour positions and insertion cell vertices.

2. **Targeted repro for failing tests:**
   - For the planar/slope field tests, capture diagnostics for the failing query points.

**Exit criteria:** We have a clear picture of when empty weights occur for interior queries.

---

### 5.2. Strengthen neighbor edge discovery

**Goal:** Ensure that interior queries find a sufficient set of natural neighbor edges.

**Tasks:**

1. **Review `GetNaturalNeighborEdges` logic:**
   - Confirm that it starts from a reasonable edge and walks appropriately, especially in regular grid-like triangulations.
   - Compare behaviour against the Rust implementation (if available) for the same point sets used in the tests.

2. **Hull-aware check:**
   - Before returning, determine whether the query lies inside the convex hull:
     - Use a simple hull test (e.g., orientation with hull vertices or a bounding box + face-based test).
   - If the query is inside the hull but `neighborEdges.Count < 2`, treat this as a failure of the edge-walk:
     - Either:
       - Retry from a different starting edge, or
       - Fall back immediately to barycentric interpolation rather than returning an empty result.

3. **Handle near-boundary cases:**
   - For queries very close to the hull, accept that neighbour sets may be small, but still ensure a non-null result using fallbacks.

**Exit criteria:** For interior points in the planar and slope field tests, `neighborEdges` is never empty; if the walk fails, we do not proceed into `GetNaturalNeighborWeights` with an empty set.

---

### 5.3. Robust weight computation and fallback

**Goal:** Even when neighbour edges are found, avoid returning `null` due to degenerate polygons or zero total area.

**Tasks:**

1. **Guard zero-area cases:**
   - In `GetNaturalNeighborWeights`, after computing `totalArea`:
     - If `totalArea == 0.0` but `neighborEdges.Count > 0`, treat this as a degeneracy.
     - In that case, bypass natural neighbour weighting and:
       - Fall back to barycentric interpolation for `Interpolate`, or
       - For `InterpolateGradient`, either:
         - Use barycentric interpolation of the underlying scalar field, or
         - Use an estimate based on `EstimateGradient` plus barycentric blending.

2. **Ensure consistent fallbacks:**
   - `Interpolate` already wraps `GetWeights` in a `try/catch` and falls back to barycentric in some error paths.
   - Extend `InterpolateGradient` to:
     - Catch natural neighbour failures (empty weights, exceptions, or zero `totalArea`), and
     - Fall back to barycentric interpolation of the scalar field when appropriate.

3. **Update tests if needed:**
   - If Rust’s natural neighbour implementation yields slightly different values near boundaries, adjust test tolerances to allow for fallback-based approximations, as long as the results remain within reasonable error bounds.

**Exit criteria:** Both `Interpolate` and `InterpolateGradient` produce non-null values for the planar/slope field tests inside the hull; tests pass with acceptable tolerances.

---

### 5.4. Regression checks

**Goal:** Verify that natural neighbour robustness improvements do not break other features.

**Tasks:**

1. **Re-run all interpolation tests:**
   - `GradientInterpolationTests`
   - `InterpolationTests` (natural neighbour + barycentric)

2. **Spot-check other uses of `NaturalNeighborInterpolator`:**
   - Ensure performance remains acceptable for small/medium triangulations.

**Exit criteria:** All interpolation tests pass; no regressions appear elsewhere.

---

## 6. Acceptance Criteria

This RFC is considered **complete** when:

1. `InterpolationTests.NaturalNeighbor_SlopeField_MatchesExpectedXWithinHull` and `GradientInterpolationTests.InterpolateGradient_UsingEstimatedGradients_MatchesPlanarField` both pass.
2. For interior queries in those tests, natural neighbour interpolation never returns `null` (it may fall back to barycentric when necessary).
3. No new failures are introduced in other interpolation or topology tests.
4. Any known remaining edge cases (e.g., on the hull boundary) are documented in comments or related RFCs.

---
