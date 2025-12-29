# Design Document: Deterministic Indexing for Clipped Voronoi

## Overview

This design implements deterministic indexing and tie-breaking for Spade.NET's clipped Voronoi diagrams. The goal is to ensure that downstream consumers can reliably map Voronoi cells to their generator site indices, with explicit diagnostics for degenerate cases and guaranteed reproducibility.

The implementation modifies `ClippedVoronoiCell`, `ClippedVoronoiDiagram`, and `ClippedVoronoiBuilder` in the `Spade.Advanced` library.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    ClippedVoronoiBuilder                        │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │ ClipToPolygon(triangulation, domain)                    │   │
│  │   - Iterates generators in index order                  │   │
│  │   - Clips each cell via half-plane intersection         │   │
│  │   - Records diagnostics for degenerate/outside cells    │   │
│  │   - Returns ClippedVoronoiDiagram                       │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                   ClippedVoronoiDiagram<V>                      │
│  ┌─────────────────┐  ┌─────────────────┐  ┌────────────────┐  │
│  │ Cells (ordered) │  │ DegenerateCells │  │ OutsideDomain  │  │
│  │ by GeneratorIdx │  │ (diagnostics)   │  │ (diagnostics)  │  │
│  └─────────────────┘  └─────────────────┘  └────────────────┘  │
│                                                                 │
│  Methods:                                                       │
│  - TryGetCell(int generatorIndex, out cell)                    │
│  - HasValidCell(int generatorIndex)                            │
│  - Indexer: this[int generatorIndex]                           │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                   ClippedVoronoiCell<V>                         │
│  - Generator: V                                                 │
│  - GeneratorIndex: int  (NEW)                                   │
│  - Polygon: IReadOnlyList<Point2<double>>                      │
│  - IsClipped: bool                                              │
└─────────────────────────────────────────────────────────────────┘
```

## Components and Interfaces

### ClippedVoronoiCell<V> (Modified)

```csharp
public sealed class ClippedVoronoiCell<TVertex>
{
    public TVertex Generator { get; }
    public int GeneratorIndex { get; }  // NEW: Original triangulation vertex index
    public IReadOnlyList<Point2<double>> Polygon { get; }
    public bool IsClipped { get; }
}
```

### ClippedVoronoiDiagram<V> (Modified)

```csharp
public sealed class ClippedVoronoiDiagram<TVertex>
{
    public ClipPolygon Domain { get; }
    public IReadOnlyList<ClippedVoronoiCell<TVertex>> Cells { get; }
    
    // NEW: Diagnostic collections
    public IReadOnlyList<int> DegenerateCells { get; }
    public IReadOnlyList<int> OutsideDomain { get; }
    
    // NEW: Direct lookup methods
    public bool TryGetCell(int generatorIndex, out ClippedVoronoiCell<TVertex>? cell);
    public bool HasValidCell(int generatorIndex);
    public ClippedVoronoiCell<TVertex>? this[int generatorIndex] { get; }
}
```

### ClippedVoronoiBuilder (Modified)

The builder will be updated to:
1. Process generators in index order (0, 1, 2, ...)
2. Track degenerate and outside-domain cases
3. Build a lookup dictionary for O(1) cell access
4. Document tie-breaking behavior in XML comments

## Data Models

### CellDiagnostic (New Internal Type)

```csharp
internal enum CellDiagnosticReason
{
    Degenerate,    // Cell has < 3 vertices after clipping
    OutsideDomain  // Generator lies entirely outside clipping domain
}
```

### Internal Lookup Structure

```csharp
// Inside ClippedVoronoiDiagram
private readonly Dictionary<int, int> _indexToCell;  // generatorIndex -> cells array index
```

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Generator index round-trip

*For any* triangulation and clipping domain, if a cell is produced for generator index `i`, then `cell.GeneratorIndex` equals `i` and `cell.Generator` equals the vertex data at index `i` in the original triangulation.

**Validates: Requirements 1.1, 1.2**

### Property 2: Cell ordering invariant

*For any* clipped Voronoi diagram, iterating over `Cells` yields cells with strictly ascending `GeneratorIndex` values.

**Validates: Requirements 1.3**

### Property 3: HasValidCell consistency

*For any* clipped Voronoi diagram and generator index `i`, `HasValidCell(i)` returns true if and only if `TryGetCell(i, out _)` returns true and the cell is not in `DegenerateCells`.

**Validates: Requirements 2.3**

### Property 4: ClipToPolygon idempotence

*For any* triangulation and clipping domain, calling `ClipToPolygon` twice with identical inputs produces identical results: same cell count, same cell polygons (within epsilon), same ordering, and same diagnostic collections.

**Validates: Requirements 3.1, 3.2, 3.3**

### Property 5: Orient2D consistency

*For any* three points (including near-collinear configurations), repeated calls to `RobustPredicates.Orient2D` with the same inputs return the same sign.

**Validates: Requirements 4.2**

### Property 6: TryGetCell correctness

*For any* clipped Voronoi diagram:
- If `generatorIndex` is valid and has a non-degenerate cell, `TryGetCell` returns true with the correct cell
- If `generatorIndex` is invalid (negative or >= numVertices) or degenerate, `TryGetCell` returns false without throwing

**Validates: Requirements 5.1, 5.2, 5.3**

## Error Handling

| Scenario | Behavior |
|----------|----------|
| Generator outside domain | Cell not created; index added to `OutsideDomain` |
| Cell becomes degenerate | Cell not created; index added to `DegenerateCells` |
| Invalid index to TryGetCell | Returns false, out parameter is null |
| Invalid index to indexer | Returns null |
| Null triangulation/domain | Throws `ArgumentNullException` |

## Testing Strategy

### Property-Based Testing

The implementation will use **FsCheck** (via FsCheck.Xunit) for property-based testing, consistent with the existing test infrastructure in Spade.Tests.

Each correctness property will be implemented as a property-based test:

1. **Generator index round-trip**: Generate random point sets, build triangulation, clip, verify indices match.
2. **Cell ordering invariant**: Generate random diagrams, verify ascending order.
3. **HasValidCell consistency**: Generate diagrams with edge cases, verify consistency.
4. **ClipToPolygon idempotence**: Generate random inputs, call twice, compare results.
5. **Orient2D consistency**: Generate near-collinear points, call repeatedly, verify same sign.
6. **TryGetCell correctness**: Generate diagrams, test valid/invalid/degenerate indices.

### Unit Tests

Unit tests will cover:
- Basic functionality with simple triangulations
- Edge cases: single point, two points, collinear points
- Boundary cases: generators on domain edges
- Degenerate cases: generators outside domain

### Test Configuration

- Property tests: minimum 100 iterations per property
- Epsilon for polygon comparison: 1e-12
- Test annotation format: `// **Feature: deterministic-indexing, Property N: <description>**`
