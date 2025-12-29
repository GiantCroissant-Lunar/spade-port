# Requirements Document

## Introduction

This feature implements deterministic indexing and tie-breaking for Spade.NET's clipped Voronoi diagrams, ensuring that downstream consumers (e.g., fanta-world's hierarchical subdivision) can reliably map Voronoi cells to their generator site indices. The current implementation produces correct cells but lacks explicit guarantees about ordering, degenerate cell handling, and tie-breaking behavior.

This work addresses RFC-018 Section 3.2 "Deterministic indexing and tie-breaking (Integration correctness)".

## Glossary

- **Generator**: A point (site) that defines a Voronoi cell; the cell contains all points closer to that generator than to any other generator.
- **Generator Index**: The integer index assigned to a vertex when inserted into the triangulation (via `vertex.Handle.Index`).
- **Clipped Voronoi Cell**: A Voronoi cell intersected with a bounding domain polygon.
- **Degenerate Cell**: A cell that has fewer than 3 vertices after clipping (effectively empty or a line).
- **Site Index**: Synonym for Generator Index in the context of Voronoi diagrams.
- **Tie-breaking**: Deterministic resolution of ambiguous geometric cases (e.g., collinear points, cocircular points).
- **ClippedVoronoiDiagram**: The result type containing all clipped cells for a triangulation.
- **ClippedVoronoiCell**: A single cell in the clipped diagram, containing the generator and polygon vertices.

## Requirements

### Requirement 1

**User Story:** As a worldgen pipeline developer, I want each clipped Voronoi cell to include its generator's original index, so that I can map cells back to my input site array without ambiguity.

#### Acceptance Criteria

1. WHEN a ClippedVoronoiCell is created THEN the ClippedVoronoiCell SHALL include a GeneratorIndex property containing the original triangulation vertex index.
2. WHEN accessing a cell's GeneratorIndex THEN the ClippedVoronoiDiagram SHALL return the same index that was assigned when the generator vertex was inserted into the triangulation.
3. WHEN iterating over ClippedVoronoiDiagram.Cells THEN the ClippedVoronoiDiagram SHALL return cells in ascending GeneratorIndex order.

### Requirement 2

**User Story:** As a worldgen pipeline developer, I want to know when a generator's cell is degenerate or missing, so that I can handle edge cases in my terrain generation.

#### Acceptance Criteria

1. WHEN a generator's cell becomes degenerate (fewer than 3 vertices after clipping) THEN the ClippedVoronoiBuilder SHALL include a diagnostic entry for that generator in a DegenerateCells collection.
2. WHEN a generator lies entirely outside the clipping domain THEN the ClippedVoronoiBuilder SHALL include a diagnostic entry for that generator in an OutsideDomain collection.
3. WHEN querying the ClippedVoronoiDiagram THEN the ClippedVoronoiDiagram SHALL provide a method to check if a specific generator index has a valid cell.

### Requirement 3

**User Story:** As a worldgen pipeline developer, I want repeated runs with identical input to produce identical output, so that my procedural generation is reproducible.

#### Acceptance Criteria

1. WHEN ClipToPolygon is called multiple times with identical triangulation and domain THEN the ClippedVoronoiBuilder SHALL produce identical cell polygons (vertex positions within 1e-12 epsilon).
2. WHEN ClipToPolygon is called multiple times with identical triangulation and domain THEN the ClippedVoronoiBuilder SHALL produce identical cell ordering.
3. WHEN ClipToPolygon is called multiple times with identical triangulation and domain THEN the ClippedVoronoiBuilder SHALL produce identical diagnostic collections.

### Requirement 4

**User Story:** As a library maintainer, I want tie-breaking behavior documented and consistent, so that near-degenerate geometric cases produce predictable results.

#### Acceptance Criteria

1. WHEN two points are equidistant from a bisector line (within floating-point precision) THEN the ClippedVoronoiBuilder SHALL use a deterministic tie-breaker based on coordinate comparison (X then Y).
2. WHEN near-collinear points produce ambiguous orientation results THEN the RobustPredicates SHALL return consistent signs across repeated evaluations.
3. WHEN the clipping algorithm encounters numerical edge cases THEN the ClippedVoronoiBuilder SHALL document the tie-breaking strategy in XML documentation comments.

### Requirement 5

**User Story:** As a library consumer, I want to query cells by generator index directly, so that I can efficiently look up specific cells without iterating.

#### Acceptance Criteria

1. WHEN a valid generator index is provided THEN the ClippedVoronoiDiagram SHALL return the corresponding cell via an indexer or TryGetCell method.
2. WHEN an invalid generator index is provided THEN the ClippedVoronoiDiagram SHALL return null or false (for TryGetCell) without throwing an exception.
3. WHEN a generator index corresponds to a degenerate cell THEN the ClippedVoronoiDiagram SHALL return null or false and the caller can check DegenerateCells for diagnostics.
