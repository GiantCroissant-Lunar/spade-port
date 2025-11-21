# RFC-007: Fix Infinite Loop in Point Location

## Status
- [ ] Reproduction
- [ ] Investigation
- [ ] Fix
- [ ] Verification

## Problem
When integrating Spade with `fantasy-map-generator`, an infinite loop occurs during the Voronoi generation phase, specifically when inserting points. This is likely happening in the point location strategy (`LocateWithHintFixedCore` or `WalkToNearestNeighbor`).

## Hypothesis
1. **Precision Issues**: Floating point errors in `SideQuery` might cause the walk to cycle between faces.
2. **Edge Cases**: Points exactly on edges or vertices might not be handled correctly in the C# port compared to Rust.
3. **Infinite Loop in Walk**: The `WalkToNearestNeighbor` might be visiting the same faces repeatedly.

## Plan
1. Reproduce the issue using `FantasyMapGenerator.Benchmarks`.
2. Add logging to trace the execution flow in `TriangulationBase.cs`.
3. Identify the cycle.
4. Implement a fix (e.g., robust predicates, cycle detection, or logic correction).
