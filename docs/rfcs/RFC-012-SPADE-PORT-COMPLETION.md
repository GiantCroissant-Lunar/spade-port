# RFC-012: Spade .NET Port Completion (Library-Only)

**Status:** Draft → Active once merged  
**Phase:** Completion / Validation  
**Scope:** `spade-port/dotnet/src/Spade` + `Spade.Tests` only (no FMG adapter or consumer code)

---

## 1. Goal

Bring the C# Spade port to a **clearly finished, self-contained state** as a library:

- Faithful to the Rust `spade` reference for the subset of features we rely on.  
- Robust (no known correctness bugs, especially in `LocateWithHintFixedCore` and flood-fill style iterators).  
- Covered by meaningful tests and oracle comparisons.  
- Documented sufficiently for reuse outside FMG.

This RFC **does not** cover integration into `fantasy-map-generator` or any specific consumer. Those can build on this RFC but are tracked separately.

---

## 2. Background

We have:

- Core DCEL, Delaunay, CDT, refinement, Voronoi, and advanced power-diagram features implemented.  
- Robust predicates (`RobustPredicates.Orient2D` / `Incircle`) wired into `MathUtils.SideQuery` and `ContainedInCircumference`.  
- A substantial test suite (Delaunay, refinement, Voronoi, power diagrams, validation/oracle tests, FMG locate regression harness).

We are now in the **“last 10%”** of porting: chasing subtle locate/topology mismatches vs the Rust reference and tightening validation/oracle coverage.

---

## 3. Deliverables

By the end of this RFC, the Spade .NET library should have:

- **D1. Robust point location parity**
  - `LocateWithHintFixedCore` behaves consistently with:
    - A brute-force face classifier in C#, and
    - The Rust `spade` implementation (for the inputs we care about).
  - Known problematic cases (e.g. UE3/UE4 rectangle-center scenario) are captured as passing regression tests.

- **D2. Strengthened validation/oracle tests**
  - Additional tests that:
    - Compare C# vs Rust outputs for selected configurations (triangulation topology, Voronoi, constrained cases).  
    - Exercise locate/flood-fill behaviour more directly.

- **D3. Stress & robustness checks**
  - Targeted tests for:
    - Near-collinear point sets.  
    - Larger point counts (e.g. 10k) in unconstrained and constrained triangulations (smoke tests, not micro-benchmarks).

- **D4. Documentation updates**
  - A short section added to existing docs (e.g. MIGRATION or USAGE) describing:
    - Guarantees / non-goals of the C# port.  
    - Notes on robust predicates and how they relate to the Rust reference.  
    - Any remaining known limitations.

---

## 4. Non-Goals

- Replacing Triangle.NET or NetTopologySuite in any specific application (FMG or otherwise).  
- Shipping a polished NuGet package and CI pipeline (covered elsewhere).  
- Implementing brand-new algorithms beyond the Rust Spade feature set.

---

## 5. Work Items

### 5.1. Brute-Force Locate Helper & Tests

**Problem:** In some constrained configurations (e.g. UE3/UE4 rectangle interior), we suspect `LocateWithHintFixedCore` may start from, or converge to, a different inner face than the Rust algorithm, even though the DCEL topology is otherwise correct.

**Tasks:**

1. **Add a test-only brute-force locator** for `Point2<double>` triangulations:
   - Input: triangulation + query point.  
   - Implementation: iterate all inner faces, use robust `Orient2D` to classify the point as inside/on-edge/outside; return a `PositionInTriangulation` value.
   - Scope: internal to `Spade.Tests` or a small test helper type; not part of the public API.

2. **Comparison tests:**
   - For a few carefully constructed triangulations (including the UE3/UE4 rectangle case):
     - Call `LocateWithHintOptionCore(center, null)` and the brute-force helper.  
     - Assert they classify the center into the same face/edge/vertex category and, where applicable, the same face handle.

3. **Diagnostics hook (optional):**
   - When a mismatch is detected, log minimal DCEL/face/edge information to aid debugging (guarded by an env var, similarly to `SPADE_DIAG_LOCATE`).

**Exit criteria:** No known cases where brute-force and `LocateWithHintFixedCore` disagree on classification for the curated test set.

---

### 5.2. Align LocateWithHintFixedCore with Brute-Force Behaviour

**Problem:** If 5.1 reveals mismatches, we need to adjust the walking logic in `LocateWithHintFixedCore` so it agrees with the robust, brute-force semantics.

**Tasks:**

1. **Analyse mismatches:**
   - For each failing test, record:
     - Start vertex / hint.  
     - Sequence of visited edges/faces.  
     - Final classification vs brute-force.

2. **Identify root causes:** likely categories:
   - Start vertex selection / hint usage.  
   - Handling of `IsOnLine` / collinear steps.  
   - Transition logic between edges (e0/e1/e2) and rotation direction (CW/CCW).

3. **Patch `LocateWithHintFixedCore`:**
   - Keep the structure close to the Rust original, but adjust where C# differences (e.g. floating-point behaviour) demand it.  
   - Add targeted unit tests that lock in the corrected behaviour.

**Exit criteria:**

- All comparison tests from 5.1 pass.  
- FMG regression CSV (if available) can be replayed without triggering locate failures or malformed DCEL invariants inside the Spade tests.

---

### 5.3. Additional Oracle/Validation Tests

We already have validation/oracle tests (e.g. JSON-based oracles, Voronoi/Delaunay comparison). This step extends coverage with a small number of **high-value scenarios**.

**Tasks:**

1. **Rust vs C# comparison for small deterministic inputs:**
   - Reuse or extend the existing `Validation/*` tests to include:
     - A few constrained polygon cases (rectangles, L-shapes) with interior points.  
     - A few Voronoi/power-diagram configurations.

2. **Property-style checks:**
   - For random point sets (within a bounded region):
     - Assert Delaunay invariants (using `ContainedInCircumference`).  
     - Assert basic topology invariants (no broken face rings, every undirected edge has two directed edges, etc.).

3. **Document any unavoidable discrepancies:**
   - If there are benign differences vs Rust (e.g. different but equivalent triangulations due to tie-breaking), capture them in comments/doc rather than trying to force bit-for-bit identity.

**Exit criteria:**

- New tests pass reliably.  
- We have at least one or two direct Rust-vs-C# comparison tests that cover constrained + Voronoi cases, not just plain Delaunay.

---

### 5.4. Stress and Robustness Smoke Tests

**Goal:** Demonstrate that the port remains stable (no exceptions, no broken invariants) under larger and more adversarial inputs.

**Tasks:**

1. **Size stress tests:**
   - Unconstrained triangulation with ~10k random points.  
   - Constrained triangulation with a modest number of constraints and ~5k points.  
   - These tests can be marked as slower and optionally skipped by default if necessary.

2. **Near-collinearity / degeneracy tests:**
   - Point sets with many nearly-collinear triples and near-co-circular points, verifying that:
     - No infinite loops.  
     - DCEL invariants hold (via existing validation helpers).

**Exit criteria:**

- Stress tests pass on the reference development machine without timeouts or invariant violations.  
- Any discovered issues are either fixed or documented as explicit limitations.

---

### 5.5. Documentation Touch-Ups

**Tasks:**

1. **Add a short "Port Completion" section** to an appropriate doc (e.g. MIGRATION or USAGE):
   - What subset of Rust Spade is ported and considered stable.  
   - Where behaviour may intentionally differ (e.g. numeric edge cases).  
   - Pointers to validation/oracle tests for future maintainers.

2. **Update CODE_REVIEW / RFC references if needed:**
   - Note that robust predicates and refinement are now in place.  
   - Mark this RFC as completed when all work items are done.

**Exit criteria:**

- Someone reading only the docs/RFCs can understand the current state and guarantees of the C# port.

---

## 6. Acceptance Criteria

This RFC is considered **complete** when:

1. All work items in sections 5.1–5.5 are implemented and tested.  
2. There are **no known correctness bugs** in core triangulation, locate, refinement, or Voronoi features under typical FMG-style inputs.  
3. The test suite (including new comparison and stress tests) passes consistently.  
4. Documentation reflects the finished state of the port.

---

## 7. Suggested Order of Execution

1. Implement 5.1 (brute-force locate helper + comparison tests).  
2. Use mismatches to drive 5.2 (patching `LocateWithHintFixedCore`).  
3. Extend validation/oracle tests (5.3) while bugs are still fresh in mind.  
4. Add stress tests (5.4) to catch any remaining edge cases.  
5. Finish with documentation touch-ups (5.5) and mark this RFC as completed in the repo history.
