# RFC-010: Spade Extensions Project Structure (Spade.Advanced)

**Status:** Draft
**Phase:** Architecture / Packaging
**Complexity:** ⭐ Medium
**Dependencies:** RFC-001 (Master Strategy), RFC-008 (Advanced Voronoi & Power Diagrams), RFC-009 (Validation & Oracle Testing)

---

## Goal

Define the **project and packaging structure** for features that go beyond the core Rust Spade port, such as:

- Advanced Voronoi and power diagram functionality (RFC-008)
- Validation and diagnostic helpers (RFC-009)
- Future experimental algorithms and utilities

The primary decision: **keep everything in the core `Spade` assembly**, or create a separate **`Spade.Advanced` (or `Spade.Extensions`) assembly**.

---

## Options

### Option A: Single Core Assembly (Spade Only)

All functionality lives in the existing `Spade` project/assembly.

**Pros:**
- Simpler dependency graph for consumers
- One package to reference
- No need to manage cross-assembly internal APIs

**Cons:**
- Mixes core Rust-parity code with experimental / higher-level features
- Harder to reason about which parts correspond to upstream Spade
- Package may grow significantly in size and API surface

### Option B: Core + Extensions Assembly (Spade + Spade.Advanced)

Maintain the current `Spade` assembly as the **faithful port** of the Rust crate, and introduce a new `Spade.Advanced` assembly for optional, higher-level features.

**Pros:**
- Clear separation of concerns:
  - `Spade`: core triangulation, CDT, refinement, basic Voronoi
  - `Spade.Advanced`: clipped/centroidal Voronoi, power diagrams, validation helpers, etc.
- Core assembly remains close to Rust Spade in behavior and surface.
- Consumers can opt-in to advanced features as needed.
- Easier to experiment in `Spade.Advanced` without destabilizing the core.

**Cons:**
- Slightly higher complexity for consumers (two package references instead of one).
- Need to manage version compatibility between `Spade` and `Spade.Advanced`.

**This RFC recommends Option B.**

---

## Proposed Structure (Option B)

### Projects & Paths

Under `dotnet/src/`:

```text
Spade/               # Existing core library (unchanged in principle)
Spade.Advanced/      # New extensions library
```

Example project files:

- `dotnet/src/Spade/Spade.csproj`
- `dotnet/src/Spade.Advanced/Spade.Advanced.csproj`

`Spade.Advanced` references `Spade`, but not vice versa.

### Namespaces

- `Spade.*` (core):
  - `Spade.Primitives`
  - `Spade.Handles`
  - `Spade.DCEL`
  - `Spade.Triangulation`
  - `Spade.Refinement`
  - `Spade.CDT`
  - `Spade.Voronoi`

- `Spade.Advanced.*` (extensions):
  - `Spade.Advanced.Voronoi`
  - `Spade.Advanced.Power`
  - `Spade.Advanced.Validation`
  - `Spade.Advanced.Utils` (as needed)

This keeps the root `Spade` namespace focused and avoids polluting it with advanced types.

---

## Versioning & Packaging

### NuGet Packages

- **Core package:** `Spade` (or `Spade.Geometry`)
  - Contains the core port, closely aligned with Rust Spade.
- **Extensions package:** `Spade.Advanced`
  - Depends on the same version of `Spade`.

### Versioning Strategy

- Use **aligned versions** (e.g., `Spade` 1.1.0 and `Spade.Advanced` 1.1.0) when released together.
- Allow `Spade.Advanced` to have **patch releases** that do not bump `Spade` (e.g., 1.1.1 vs 1.1.0) as long as public APIs and compatibility are preserved.

---

## Impact on Existing Work

### Core Port (RFC-001–RFC-006)

No changes required to the **goals or structure** of the core port. All existing phases remain valid.

### Advanced Features (RFC-008)

- Implementation for advanced Voronoi and power diagrams targets the `Spade.Advanced` project.
- Core Spade remains focused on:
  - Delaunay triangulation
  - CDT / refinement
  - Basic Voronoi extraction

### Validation (RFC-009)

- Validation helpers that are
  - **Internals for core correctness** should live in `Spade` or in internal test projects.
  - **High-level validation utilities** (e.g., rich diagnostics, Oracle comparison helpers) may live in `Spade.Advanced.Validation` or in dedicated test projects.

---

## Migration & Consumer Experience

### For New Consumers

- Default: reference `Spade` only.
- If advanced functionality is needed:
  - Add reference to `Spade.Advanced`.
  - Use `Spade.Advanced.*` namespaces explicitly.

### For Existing Consumers (e.g., Fantasy Map Generator)

- Initial migration steps:
  - No immediate changes; FMG can continue to use core Spade APIs.
  - When advanced features are adopted, add `Spade.Advanced` reference.
- Documentation and examples should clearly distinguish between core and advanced usage.

---

## Implementation Plan (High Level)

### Phase 1: Define Structure & Contracts (This RFC)

- [ ] Agree on Option B (Spade + Spade.Advanced).
- [ ] Confirm project paths and namespaces.

### Phase 2: Skeleton Projects

- [ ] Create `Spade.Advanced` project and wire references to `Spade`.
- [ ] Add minimal placeholder types/namespaces (no heavy implementation):
  - `Spade.Advanced.Voronoi`
  - `Spade.Advanced.Power`
  - `Spade.Advanced.Validation`

### Phase 3: Feature Integration

- [ ] Implement RFC-008 features inside `Spade.Advanced`.
- [ ] Integrate validation tooling from RFC-009 (where appropriate).

### Phase 4: Packaging & Documentation

- [ ] Add NuGet metadata for `Spade.Advanced`.
- [ ] Document when to use `Spade` vs `Spade.Advanced` in `USAGE.md` and API docs.

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|-----------|
| Package sprawl (too many assemblies) | Low | Limit to core + advanced; avoid additional top-level packages |
| Confusion about where to add new features | Medium | Document clear guidelines: core mirrors Rust, advanced is for extras |
| Version skew between Spade and Spade.Advanced | Medium | Use aligned versions; CI checks to ensure compatible combinations |

---

## Acceptance Criteria

- ✅ Decision documented between single-assembly and dual-assembly structure.
- ✅ Clear mapping of feature areas to `Spade` vs `Spade.Advanced`.
- ✅ High-level project and namespace structure agreed.
- ✅ Ready to create a `Spade.Advanced` project skeleton and begin implementing RFC-008/RFC-009 work there.
