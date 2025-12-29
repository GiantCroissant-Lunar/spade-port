# Implementation Plan

- [x] 1. Update ClippedVoronoiCell to include GeneratorIndex





  - [x] 1.1 Add GeneratorIndex property to ClippedVoronoiCell<TVertex>


    - Add `public int GeneratorIndex { get; }` property
    - Update internal constructor to accept generatorIndex parameter
    - Add XML documentation explaining the property's purpose
    - _Requirements: 1.1, 1.2_
  - [x] 1.2 Write property test for generator index round-trip






    - **Property 1: Generator index round-trip**
    - **Validates: Requirements 1.1, 1.2**

- [x] 2. Update ClippedVoronoiDiagram with diagnostic collections and lookup





  - [x] 2.1 Add diagnostic collections to ClippedVoronoiDiagram


    - Add `public IReadOnlyList<int> DegenerateCells { get; }` property
    - Add `public IReadOnlyList<int> OutsideDomain { get; }` property
    - Update internal constructor to accept diagnostic lists
    - _Requirements: 2.1, 2.2_
  - [x] 2.2 Add direct lookup methods to ClippedVoronoiDiagram


    - Add private `Dictionary<int, int> _indexToCell` for O(1) lookup
    - Implement `public bool TryGetCell(int generatorIndex, out ClippedVoronoiCell<TVertex>? cell)`
    - Implement `public bool HasValidCell(int generatorIndex)`
    - Implement `public ClippedVoronoiCell<TVertex>? this[int generatorIndex]` indexer
    - _Requirements: 2.3, 5.1, 5.2, 5.3_
  - [x] 2.3 Write property test for HasValidCell consistency






    - **Property 3: HasValidCell consistency**
    - **Validates: Requirements 2.3**
  - [x] 2.4 Write property test for TryGetCell correctness






    - **Property 6: TryGetCell correctness**
    - **Validates: Requirements 5.1, 5.2, 5.3**

- [x] 3. Update ClippedVoronoiBuilder for deterministic ordering and diagnostics





  - [x] 3.1 Modify ClipToPolygon to process generators in index order


    - Ensure iteration over generators uses ascending index order
    - Pass generatorIndex to ClippedVoronoiCell constructor
    - _Requirements: 1.3_
  - [x] 3.2 Add diagnostic tracking to ClipToPolygon


    - Create lists for degenerate and outside-domain generators
    - Record generator index when cell is skipped due to < 3 vertices
    - Record generator index when generator is outside domain
    - Pass diagnostic lists to ClippedVoronoiDiagram constructor
    - _Requirements: 2.1, 2.2_
  - [x] 3.3 Document tie-breaking behavior in XML comments


    - Add XML documentation to ClipPolygonToBisectorHalfPlane explaining epsilon handling
    - Document coordinate-based tie-breaking strategy
    - _Requirements: 4.1, 4.3_
  - [x] 3.4 Write property test for cell ordering invariant






    - **Property 2: Cell ordering invariant**
    - **Validates: Requirements 1.3**

- [x] 4. Checkpoint - Ensure all tests pass





  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Add idempotence and consistency tests





  - [x] 5.1 Write property test for ClipToPolygon idempotence








    - **Property 4: ClipToPolygon idempotence**
    - **Validates: Requirements 3.1, 3.2, 3.3**
  - [x] 5.2 Write property test for Orient2D consistency













    - **Property 5: Orient2D consistency**
    - **Validates: Requirements 4.2**

- [x] 6. Add unit tests for edge cases





  - [x] 6.1 Write unit tests for degenerate cell scenarios






    - Test generator outside domain
    - Test generator on domain boundary
    - Test cell that clips to < 3 vertices
    - _Requirements: 2.1, 2.2_
  - [x] 6.2 Write unit tests for lookup methods





    - Test TryGetCell with valid index
    - Test TryGetCell with invalid index (negative, out of range)
    - Test indexer behavior
    - _Requirements: 5.1, 5.2, 5.3_

- [x] 7. Final Checkpoint - Ensure all tests pass





  - Ensure all tests pass, ask the user if questions arise.
