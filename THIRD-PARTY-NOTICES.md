# Third-Party Notices

Spade for .NET builds on ideas and reference implementations from several
open-source projects. Snapshots of these projects are kept under
`ref-projects/` for testing, validation, and comparison.

Their original license files are preserved in those directories and should be
consulted for the full terms. A brief overview:

- **JuliaGeometry/DelaunayTriangulation.jl**
  - License: MIT
  - Local path: `ref-projects/DelaunayTriangulation.jl`
  - See: `ref-projects/DelaunayTriangulation.jl/LICENSE`

- **Stoeoef/spade** and **robust** (Rust crates)
  - Licenses: MIT and Apache License 2.0
  - Local paths: `ref-projects/spade`, `ref-projects/robust`
  - See: `ref-projects/spade/LICENSE-MIT`, `ref-projects/spade/LICENSE-APACHE`,
    `ref-projects/robust/LICENSE-MIT`, `ref-projects/robust/LICENSE-APACHE`

- **robust-predicates**
  - License: The Unlicense (public domain dedication)
  - Local path: `ref-projects/robust-predicates`
  - See: `ref-projects/robust-predicates/LICENSE`

- **RobustGeometry.NET** and other components used by voronator-sharp**
  - Collected under: `ref-projects/voronator-sharp`
  - See: `ref-projects/voronator-sharp/LICENSE.txt` for the bundled
    third-party license texts (including RobustGeometry.NET, d3-delaunay,
    and DelaunatorSharp).

This C# port itself is licensed under the MIT License (see `LICENSE`). The
notices above are informational and do not supersede the original upstream
license texts.
