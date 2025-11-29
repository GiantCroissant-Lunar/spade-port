# Handover: Spade Voronoi issues in Fantasy Map Generator port

**Audience:** Agent working on `dotnet/_lib/spade-port`
**Goal:** Make the Spade Voronoi backend robust enough that the FMG port can use it instead of the current NTS fallback, without changing public Voronoi / map contracts.

---

## 1. Problem Summary

The Fantasy Map Generator .NET port (`dotnet/_lib/fantasy-map-generator-port`) has two Voronoi backends:

- **NTS backend** – implemented in `FantasyMapGenerator.Core/Geometry/Voronoi.cs`
  - Works correctly and is used as the current reference.
- **Spade backend** – implemented in `FantasyMapGenerator.Core/Geometry/SpadeAdapter.cs`
  - Produces **severe visual artifacts** when used from FMG:
    - When rendered with a debug overlay, Voronoi cells become a dense **“hairball”** of long chords across the map instead of clean polygon rings.
    - Neighbor counts / basic stats still look plausible, so the issue is geometric, not purely topological.

Because of this, the FMG game-side provider (`PigeonPea.Plugin.Map.FMG/FmgMapProvider.cs`) is currently hard‑wired to `VoronoiBackend.Nts`. The user would like to switch back to Spade once it produces sane Voronoi cells.

---

## 2. Repro Instructions (Single-Project Harness)

Use the dedicated FMG PNG export tool – this avoids the game HUD and isolates the backend:

**Project:**
`dotnet/_lib/fantasy-map-generator-port/dotnet/tools/FmgExportPng`

1. Open `Program.cs` in that project.
2. Ensure the generator settings use the Spade backend:
   ```csharp
   var settings = new MapGenerationSettings
   {
       Width = 2048,
       Height = 2048,
       NumPoints = 2000,
       HeightmapTemplate = "continents",
       VoronoiBackend = VoronoiBackend.Spade, // <— important
       Seed = 123456
   };
   ```
3. Run:
   ```pwsh
   cd dotnet/_lib/fantasy-map-generator-port
   dotnet run --project dotnet/tools/FmgExportPng
   ```
4. The tool writes a PNG under `dotnet/tools/FmgExportPng/bin/.../export/`, e.g. `fmg-map-123456.png`.
5. The exporter already has a `DrawVoronoiOverlay(mapData, canvas)` helper that:
   - Uses `mapData.Voronoi.Cells.Vertices[i]` and `mapData.Voronoi.Vertices.Coordinates` to draw cell rings.
   - Draws **cell centers as small red dots**.

With `VoronoiBackend.Spade` you’ll see the “hairball” overlay. With `VoronoiBackend.Nts` (just flip the enum) you get clean Voronoi cells. Use these two outputs as A/B reference while you work.

---

## 3. Relevant Code Paths

### 3.1 FMG Voronoi + MapData

- `dotnet/_lib/fantasy-map-generator-port/dotnet/src/FantasyMapGenerator.Core/Generators/MapGenerator.cs`
  - `GenerateVoronoiDiagram()` calls either the NTS or Spade backend:
    - NTS: `Voronoi.FromPoints(points, width, height)`
    - Spade: `SpadeAdapter.GenerateVoronoi(points, width, height)`
  - The resulting `Voronoi` instance is then used to populate `MapData.Voronoi`.

- Public Voronoi contracts (do **not** change):
  - `Voronoi.Cells.Vertices` – per-cell list of indices into `Voronoi.Vertices.Coordinates`.
  - `Voronoi.Cells.Neighbors` – per-cell neighbor indices.
  - `Voronoi.Cells.IsBorder` – whether the cell touches the boundary.
  - `Voronoi.Vertices.Coordinates` – list of vertex positions (`GeoPoint`).

### 3.2 NTS Backend (Reference / Gold Standard)

- `dotnet/_lib/fantasy-map-generator-port/dotnet/src/FantasyMapGenerator.Core/Geometry/Voronoi.cs`
  - Uses NTS (`NetTopologySuite`) to build a bounded Voronoi diagram.
  - Explicitly clips to the `[0,width] × [0,height]` envelope.
  - Produces clean, ordered rings in `Cells.Vertices`.

This backend is **known-good** and should be used to compare against Spade behavior for the same point set and seed.

### 3.3 Spade Backend (Fix Target)

- `dotnet/_lib/fantasy-map-generator-port/dotnet/src/FantasyMapGenerator.Core/Geometry/SpadeAdapter.cs`
  - Core method: `GenerateVoronoi(List<Point> points, double width, double height)`.
  - High-level behavior:
    1. Builds `DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>`.
    2. Inserts FMG points as Spade vertices.
       - Note: dummy/bounding vertices are currently **not** inserted (that block is commented out).
    3. For each FMG site `i`:
       - Constructs a `VoronoiFace<Point2<double>, int, int, int>` using the site’s handle.
       - Iterates `foreach (var edge in voronoiFace.AdjacentEdges())`:
         - Uses `edge.AsDelaunayEdge().To().Handle` to identify neighbor sites.
         - Uses `edge.From()` to get a `VoronoiVertex`:
           - If `Inner` → uses its `Position` (circumcenter) as a Voronoi vertex.
           - If `Outer` → marks the cell as border (`IsBorder = true`) but **does not** create a vertex.
         - Each `Inner` vertex is deduped by `inner.Face.Handle.Index` via a `faceIndexToVertexIndex` dictionary and added to `cellVertices`.
       - Assigns:
         ```csharp
         voronoi.Cells.Vertices[i]  = cellVertices;
         voronoi.Cells.Neighbors[i] = cellNeighbors;
         voronoi.Cells.IsBorder[i]  = isBorder;
         ```

Important observations from previous analysis:

- `VoronoiFace.AdjacentEdges()` in Spade is effectively a **CCW walk** around the site in the Delaunay DCEL; the order is generally suitable for a ring.
- Each inner Delaunay face corresponds to one Voronoi vertex (circumcenter), so keying by `inner.Face.Handle.Index` is conceptually sound.
- The “hairball” effect is most likely **not** due to random vertex ordering, but rather due to how border / infinite edges are handled.

---

## 4. Hypothesized Root Cause

The artifacts seen in FMG when using Spade are consistent with **border / infinite cells being forcibly closed with long chords**:

- For interior cells:
  - All incident edges are `Inner` and have finite circumcenters.
  - The vertices form a convex ring around the site; even with minimal processing the polygon looks reasonable.

- For border cells:
  - Some incident edges are `Outer` (Voronoi edges that extend to infinity).
  - The current adapter:
    - Marks `IsBorder = true`.
    - **Skips** those vertices entirely (no coordinates are added).
    - Still hands `cellVertices` to the drawing code, and the overlay closes the ring via `path.Close()`.
  - This causes:
    - Gaps where boundary edges should be.
    - The polygon to close with a “shortcut” straight segment from the last inner vertex back to the first, often spanning a large distance across the map.
    - When drawn for every border cell, these shortcuts become the dense vertical/diagonal chords that look like a hairball.

In short: **infinite Voronoi cells are being treated as finite polygons without proper clipping**, and the missing outer vertices are replaced implicitly by giant chords.

This aligns with the user’s screenshots and with the fact that NTS (which clips properly) does not show the problem.

---

## 5. Desired Outcome

For the **same input point set and seed**, Spade’s `GenerateVoronoi` should produce `Voronoi` data that is _topologically and visually equivalent_ to the NTS backend:

- `Cells.Vertices[i]`:
  - Lists vertices in **consistent ring order** (preferably CCW).
  - No self‑intersecting polygons.
  - Border cells are either:
    - Properly clipped to the rectangle `[0,width] × [0,height]`, or
    - Clearly flagged and optionally omitted from debug drawing without causing hairballs.

- `Cells.Neighbors[i]`:
  - Matches NTS neighbor counts (up to small differences due to clipping strategy).

- When drawn via the existing `DrawVoronoiOverlay`:
  - You see a clean Voronoi diagram similar to NTS (no global chords).

We **must not** change the `Voronoi` data structures or `MapGenerator.GenerateVoronoiDiagram` contract to achieve this.

---

## 6. Suggested Fix Strategy

### Step 1 – Add Quick Diagnostic Guards (Optional but Helpful)

In the FMG overlay or in a temporary diagnostic tool:

- Skip drawing cells where `Cells.IsBorder[i]` is `true` and confirm whether the hairball largely disappears.
  - If it does, that’s strong evidence that border handling is the primary issue.

This doesn’t fix the geometry, but helps validate the hypothesis while you work on the core fix.

### Step 2 – Make Border Cells Finite

You have two main options; either is acceptable as long as it yields finite, bounded cells.

#### Option A: Bounding Points / Super-Rectangle (recommended first)

Re‑introduce the “dummy points” approach that was originally sketched in `SpadeAdapter` and later commented out:

- Insert a set of vertices that form a large rectangle slightly larger than the `[0,width] × [0,height]` domain:
  - e.g. `(-padding, -padding)`, `(width + padding, -padding)`, `(width + padding, height + padding)`, `(-padding, height + padding)`.
- Ensure FMG points are inserted **before** dummy points and that `MapGenerator` only treats the original sites as map cells:
  - This is already hinted at by checks like `neighborHandle.Index < points.Count`.
- With a surrounding box, all Voronoi faces for original sites become finite; no `Outer` vertices should remain for interior faces inside the box.

You may still set `IsBorder = true` for cells that touch dummy vertices or cross the rectangle boundary, but **every cell should then have a complete ring of finite vertices**.

#### Option B: Explicit Clipping (more work, closer to NTS)

If you prefer not to use dummy points:

- For each `DirectedVoronoiEdge`:
  - When `edge.From()` is `Inner`, you have a finite circumcenter.
  - When `edge.To()` or the paired edge indicates an infinite edge, compute its intersection with the `[0,width] × [0,height]` box.
  - Add those intersection points as extra vertices and build the ring from both circumcenters and clip points.

This approach is more complex and will resemble what `Voronoi.cs` (NTS backend) does with `ClipEnvelope` and `LineString` clipping.

### Step 3 – Ring Ordering (If Needed)

Even though `VoronoiFace.AdjacentEdges()` should already walk edges CCW, it is still safer to normalize the vertex order per cell:

For each cell `i`:

1. Collect all finite Voronoi vertices as `(vertexIndex, x, y)` from the edge walk (after border handling / clipping).
2. Deduplicate by `vertexIndex` (to share vertices across cells).
3. Compute a centroid (e.g. average of vertex positions or the original site position).
4. Sort vertices by angle around the centroid:
   ```csharp
   var cx = centroid.X;
   var cy = centroid.Y;
   cellVertices.Sort((a, b) =>
   {
       var va = vertices[a].Coordinates;
       var vb = vertices[b].Coordinates;
       var angleA = Math.Atan2(va.Y - cy, va.X - cx);
       var angleB = Math.Atan2(vb.Y - cy, vb.X - cx);
       return angleA.CompareTo(angleB);
   });
   ```
5. Store the resulting ordered list into `voronoi.Cells.Vertices[i]`.

This guarantees simple convex rings for typical Voronoi cells and makes behavior less sensitive to subtle edge-ordering quirks.

### Step 4 – Keep Contracts Intact

While implementing the above in `SpadeAdapter.GenerateVoronoi`:

- Do **not** change:
  - `Voronoi` class shape.
  - `MapGenerator.GenerateVoronoiDiagram` signature.
  - Any consumer relying on `Voronoi.Cells.Vertices` and `Voronoi.Vertices.Coordinates`.
- It’s fine to add private helpers within `SpadeAdapter` to:
  - Insert bounding points.
  - Clip edges.
  - Order vertices.

---

## 7. Validation Checklist

Once you’ve implemented the fix in `SpadeAdapter`:

1. **Rebuild and run FmgExportPng** with `VoronoiBackend.Spade`.
2. Generate a PNG for a fixed seed (e.g. `Seed = 123456`).
   - Compare visually to the NTS backend:
     - Terrain: should already match (heightmap bug was previously fixed).
     - Voronoi overlay: cells should look like a typical Voronoi diagram, not a hairball.
3. Sanity‑check metrics (you can add a temporary dump around `GenerateVoronoi`):
   - Number of cells with non‑empty `Cells.Vertices` equals `points.Count` (minus any explicitly discarded dummy sites).
   - Average neighbor count is in a reasonable range (typically 5–7 for interior cells).
   - No vertex indices out of range.
4. Optional: run the existing `spade-port` test suite to ensure no regressions in core Delaunay/Voronoi behavior.

---

## 8. Notes / Non‑Goals

- **Non‑goal:** You do not need to make Spade’s Voronoi numerically identical to NTS; small geometric differences are fine. The priority is *structurally correct, bounded cells* that render cleanly.
- **Non‑goal:** You do not need to modify the FMG Windows HUD (`PigeonPea.Windows.MapHud`) as part of this task. That code already relies on `MapData.Voronoi` and will benefit automatically once the backend is fixed.
- If you need more context on how FMG consumes Voronoi data, see the FMG docs in the root repo:
  - `FMG-BUG-REPORT.md`
  - `FMG-FIX-INSTRUCTIONS.md`
  - `FMG-RIVER-ISSUE-ANALYSIS.md`

If you have to choose between a simpler but slightly approximate solution (e.g. bounding rectangle with mild distortion near the edges) and a complex exact clipper, **prefer the simpler, reliable one**. The user cares most about having a clean, understandable Voronoi structure that matches the overall FMG look.
