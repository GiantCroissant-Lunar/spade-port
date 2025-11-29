# RFC-011: Robust Geometric Predicates (orient2d / incircle)

**Status:** Draft
**Phase:** Cross-cutting (applies to Phases 2–5)
**Duration:** 3–5 days (design + implementation + tests)
**Complexity:** ⭐⭐⭐ Hard
**Dependencies:** RFC-002, RFC-003, RFC-004, RFC-005

---

## Goal

Introduce robust, Shewchuk-style adaptive predicates for 2D orientation (`orient2d`) and incircle tests, and integrate them into the Spade .NET port so that:

- Orientation queries (`SideQuery`, edge walking, ray/segment intersection) are numerically reliable even for near-degenerate and large-coordinate inputs.
- Delaunay legality tests (`ContainedInCircumference`) are stable and match the Rust Spade behavior.
- DCEL traversal, CDT constraint insertion/splitting, and mesh refinement are less prone to infinite loops or topology corruption caused by floating-point rounding errors.

This RFC is *design-only* for now. Implementation can be scheduled once higher-priority Phase 3/4 items are stable.

---

## Motivation

The current .NET port uses improved but still heuristic predicates:

- `MathUtils.SideQuery` computes a double-precision determinant with a scale-aware epsilon and snaps very small values to "on line".
- `MathUtils.ContainedInCircumference` uses a direct polynomial incircle test without robustness guarantees.

These are significantly better than naive float math but still susceptible to:

- Sign flips for nearly collinear points or very large coordinates.
- Platform- or scale-dependent behavior.
- Rare but catastrophic failures in DCEL-based algorithms (e.g. line walkers, edge legalization, refinement) leading to invalid meshes or non-termination.

The Rust reference uses a dedicated `robust` module:

```rust
pub fn side_query<S>(p1: Point2<S>, p2: Point2<S>, query_point: Point2<S>) -> LineSideInfo
where
    S: SpadeNum,
{
    let p1 = to_robust_coord(p1);
    let p2 = to_robust_coord(p2);
    let query_point = to_robust_coord(query_point);

    let result = robust::orient2d(p1, p2, query_point);
    LineSideInfo::from_determinant(result)
}

pub fn contained_in_circumference<S>(
    v1: Point2<S>,
    v2: Point2<S>,
    v3: Point2<S>,
    p: Point2<S>,
) -> bool
where
    S: SpadeNum,
{
    let v1 = to_robust_coord(v1);
    let v2 = to_robust_coord(v2);
    let v3 = to_robust_coord(v3);
    let p = to_robust_coord(p);

    // incircle expects all vertices to be ordered CW for right-handed systems.
    // For consistency, the public interface of this method will expect the points to be
    // ordered ccw.
    robust::incircle(v3, v2, v1, p) < 0.0
}
```

Matching this behavior in the .NET port requires an explicit, well-scoped design.

---

## Scope

### In Scope

- Design and API for robust predicates in C#:
  - `Orient2D` equivalent for orientation tests.
  - `Incircle` equivalent for Delaunay legality.
- Integration points:
  - `MathUtils.SideQuery` (and thus `LineSideInfo`, `DirectedEdgeHandle.SideQuery`).
  - `MathUtils.ContainedInCircumference` used by edge legalization.
- Testing strategy:
  - Deterministic edge-case tests (large coordinates, nearly collinear, nearly cocircular).
  - Randomized stress tests comparing against a high-precision oracle (e.g. decimal / BigInteger-based reference) or the Rust `robust` implementation.

### Out of Scope (for this RFC)

- Changing the public Spade API surface (no new public types beyond internal helpers).
- General-purpose exact arithmetic or a full arbitrary-precision math library.
- Optimizing performance beyond basic micro-benchmarks.

---

## Design Options

### Option A: Internal RobustPredicates Module (Pure C#)

Implement a small internal `RobustPredicates` module in C#, ported conceptually from Shewchuk / the Rust `robust` crate:

- Expansion primitives (`TwoSum`, `TwoProduct`, expansion add/mul).
- `Orient2D(p1, p2, p3)`:
  - Fast double path with derived error bound.
  - Exact expansion-based recomputation when `|det|` is near the error bound.
- `Incircle(p1, p2, p3, p)` similarly using expansions.

Pros:

- No external dependencies.
- Behavior fully under Spade's control.
- Closest to the Rust Spade semantics.

Cons:

- Significant implementation complexity.
- Easy to introduce subtle bugs if the port is not done carefully.

### Option B: Depend on an Existing C# Robust Geometry Library

If a mature, permissively-licensed C# library exists that provides Shewchuk-style predicates:

- Wrap its `orient2d` / `incircle` in `MathUtils`.
- Keep call sites unchanged.

Pros:

- Less code to maintain in this repo.
- Potentially battle-tested implementation.

Cons:

- Adds an external dependency (versioning, licensing, maintenance).
- Might not match Rust Spade's exact behavior or threshold choices.

---

## Proposed API (C#)

### Internal Helper

```csharp
namespace Spade.Primitives;

internal static class RobustPredicates
{
    // Returns a signed double whose sign matches the exact orient2d result.
    // Positive: p1->p2->p3 is CCW. Negative: CW. Zero: collinear.
    public static double Orient2D(Point2<double> p1, Point2<double> p2, Point2<double> p3);

    // Signed incircle test. Sign semantics to be aligned with Rust `robust::incircle`.
    public static double Incircle(
        Point2<double> a,
        Point2<double> b,
        Point2<double> c,
        Point2<double> p);
}
```

### Integration in MathUtils

```csharp
public static LineSideInfo SideQuery<S>(Point2<S> p1, Point2<S> p2, Point2<S> queryPoint)
    where S : struct, INumber<S>, ISignedNumber<S>
{
    var d1 = new Point2<double>(double.CreateChecked(p1.X), double.CreateChecked(p1.Y));
    var d2 = new Point2<double>(double.CreateChecked(p2.X), double.CreateChecked(p2.Y));
    var dq = new Point2<double>(double.CreateChecked(queryPoint.X), double.CreateChecked(queryPoint.Y));

    var det = RobustPredicates.Orient2D(d1, d2, dq);
    return LineSideInfo.FromDeterminant(det);
}

public static bool ContainedInCircumference<S>(
    Point2<S> v1,
    Point2<S> v2,
    Point2<S> v3,
    Point2<S> p)
    where S : struct, INumber<S>, ISignedNumber<S>
{
    var d1 = new Point2<double>(double.CreateChecked(v1.X), double.CreateChecked(v1.Y));
    var d2 = new Point2<double>(double.CreateChecked(v2.X), double.CreateChecked(v2.Y));
    var d3 = new Point2<double>(double.CreateChecked(v3.X), double.CreateChecked(v3.Y));
    var dp = new Point2<double>(double.CreateChecked(p.X),  double.CreateChecked(p.Y));

    // Align sign convention with Rust Spade.
    return RobustPredicates.Incircle(d1, d2, d3, dp) > 0.0;
}
```

---

## Implementation Plan (Deferred)

1. **Research & Design (0.5–1 day)**
   - Review Rust `robust` crate and Shewchuk's original C implementation.
   - Decide between Option A (internal port) and Option B (external dependency).
   - Finalize sign conventions for `Orient2D` and `Incircle` to match Rust Spade.

2. **Prototype Orient2D (1–2 days)**
   - Implement expansion primitives (if Option A).
   - Implement `RobustPredicates.Orient2D` with:
     - Fast double path + error bound.
     - Exact expansion fallback.
   - Add targeted tests for:
     - Small and large coordinate ranges.
     - Nearly collinear triples.
     - Randomized comparisons against a high-precision oracle or Rust `orient2d`.

3. **Prototype Incircle (1–2 days)**
   - Implement `RobustPredicates.Incircle` reusing expansions.
   - Wire into `MathUtils.ContainedInCircumference`.
   - Add tests for nearly cocircular inputs and Delaunay edge cases.

4. **Integration & Regression (1 day)**
   - Replace current heuristic `SideQuery`/`ContainedInCircumference` with robust versions.
   - Run full Spade test suite and benchmarks.
   - Compare representative triangulations against Rust Spade outputs (RFC-009).

---

## Risks & Mitigations

- **Risk:** Subtle bugs in expansion arithmetic.
  - *Mitigation:* Port in small, well-tested steps; cross-check with Rust `robust` outputs.

- **Risk:** Performance regressions in hot loops.
  - *Mitigation:* Keep adaptive design (fast path most of the time); micro-benchmark before and after.

- **Risk:** Licensing / provenance of original predicate code.
  - *Mitigation:* Treat Shewchuk / `robust` as *reference algorithms*, not copied code; implement in clean-room style and document clearly in this RFC.

---

## Success Criteria

- Exact sign agreement with Rust `robust::orient2d` and `robust::incircle` for a large randomized test corpus.
- No regressions in existing Spade tests.
- Improved stability for known problematic configurations (large coordinates, near collinearity, near cocircularity).
- Documented design and clear toggle point (`RobustPredicates` + `MathUtils` integration) for future maintenance.
