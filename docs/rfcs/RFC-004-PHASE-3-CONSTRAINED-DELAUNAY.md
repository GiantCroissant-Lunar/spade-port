# Status
Completed

# Summary
Port the Constrained Delaunay Triangulation (CDT) implementation from Rust to C#.
This includes the `ConstrainedDelaunayTriangulation` class, constraint insertion logic, and necessary data structures.

# Goals
1.  Create `ConstrainedDelaunayTriangulation<V, DE, UE, F, L>` class.
2.  Implement `AddConstraint` method.
3.  Implement `LineIntersectionIterator` for constraint checking and conflict detection.
4.  Implement `ResolveConflictRegion` for flipping edges to enforce constraints.
5.  Refactor `DelaunayTriangulation` to share common logic with CDT.

# Implementation Details

## Data Structures
-   `CdtEdge<UE>`: Wrapper for undirected edge data, adding `IsConstraintEdge` flag.
-   `Intersection`: Enum/Record for line intersection types (EdgeIntersection, VertexIntersection, EdgeOverlap).

## Classes
-   `TriangulationBase<V, DE, UE, F, L>`: Base class containing common triangulation logic (insertion, location, legalization).
-   `DelaunayTriangulation<V, DE, UE, F, L>`: Inherits from `TriangulationBase`.
-   `ConstrainedDelaunayTriangulation<V, DE, UE, F, L>`: Inherits from `TriangulationBase`, using `CdtEdge<UE>`.
-   `LineIntersectionIterator`: Iterator for traversing the triangulation along a line.

## Algorithms
-   **Constraint Insertion**:
    -   Use `LineIntersectionIterator` to find edges intersecting the constraint line.
    -   If intersections found, identify the conflict region.
    -   Flip edges in the conflict region until the constraint edge can be created.
    -   Mark the new edge as a constraint.
    -   Legalize edges around the new constraint (respecting constraints).

# Progress
- [x] Create `CdtEdge.cs`.
- [x] Refactor `DelaunayTriangulation` to `TriangulationBase`.
- [x] Create `ConstrainedDelaunayTriangulation.cs`.
- [x] Implement `LineIntersectionIterator`.
- [x] Implement `AddConstraint` and conflict resolution.
- [x] Add tests for CDT.
