# RFC-004: Phase 3 - Mesh Refinement

**Status:** Draft
**Phase:** 3 of 5
**Duration:** Week 3 (5-7 days)
**Complexity:** ⭐⭐⭐ Hard
**Dependencies:** Phase 2 (RFC-003)

---

## Goal

Implement quality-constrained mesh generation (Ruppert's/Delaunay refinement algorithm).

**Critical Features:**
- ✅ Minimum angle constraints (default 20°)
- ✅ Maximum area constraints
- ✅ Steiner point insertion
- ✅ Constraint edge preservation

---

## Components to Port

### Source Files (Rust)
- `ref-projects/spade/src/refinement.rs`
- `ref-projects/spade/src/refinement_parameters.rs`

### Target Files (C#)
```
Refinement/
├── RefinementParameters.cs    # Parameters
├── AngleLimit.cs               # Angle constraints
├── RefinementResult.cs         # Result info
├── Refiner.cs                  # Main algorithm
└── SteinerInsertion.cs         # Steiner point logic
```

---

## Key Algorithm: Ruppert's Refinement

### Pseudocode
```
REFINE(mesh, params):
  skinny_queue = FIND_SKINNY_TRIANGLES(mesh, params.min_angle)
  encroached_queue = []

  WHILE skinny_queue NOT EMPTY OR encroached_queue NOT EMPTY:
    // First, handle encroached segments
    WHILE encroached_queue NOT EMPTY:
      segment = encroached_queue.pop()
      midpoint = segment.midpoint()
      INSERT_VERTEX(mesh, midpoint)
      UPDATE_QUEUES(skinny_queue, encroached_queue)

    // Then, handle skinny triangles
    IF skinny_queue NOT EMPTY:
      triangle = skinny_queue.pop()
      circumcenter = CIRCUMCENTER(triangle)

      IF ENCROACHES_SEGMENT(circumcenter):
        ADD_TO_QUEUE(encroached_queue, encroached_segment)
      ELSE:
        INSERT_VERTEX(mesh, circumcenter)
        UPDATE_QUEUES(skinny_queue, encroached_queue)

  RETURN mesh
```

### C# API

```csharp
public class RefinementParameters<S> where S : struct, ISpadeNum<S>
{
    public AngleLimit AngleLimit { get; set; } = AngleLimit.FromDegrees(30.0);
    public S? MaxAllowedArea { get; set; } = null;
    public S? MinRequiredArea { get; set; } = null;
    public bool KeepConstraintEdges { get; set; } = false;
    public bool ExcludeOuterFaces { get; set; } = false;
    public int MaxAdditionalVertices { get; set; } = int.MaxValue;

    public RefinementParameters<S> WithAngleLimit(AngleLimit limit)
    {
        AngleLimit = limit;
        return this;
    }

    public RefinementParameters<S> WithMaxAllowedArea(S area)
    {
        MaxAllowedArea = area;
        return this;
    }
}

public readonly struct AngleLimit
{
    private readonly double radians;

    public static AngleLimit FromDegrees(double degrees) =>
        new AngleLimit(degrees * Math.PI / 180.0);

    public static AngleLimit FromRadians(double radians) =>
        new AngleLimit(radians);

    public double Degrees => radians * 180.0 / Math.PI;
    public double Radians => radians;
}
```

---

## Implementation Checklist

**Week 3 Day 1-2: Parameters & Setup**
- [ ] `RefinementParameters<S>` class
- [ ] `AngleLimit` struct
- [ ] `RefinementResult` class
- [ ] Quality metrics (min angle, max area)

**Week 3 Day 3-5: Core Algorithm**
- [ ] Skinny triangle detection
- [ ] Large triangle detection
- [ ] Steiner point insertion
- [ ] Encroachment checking
- [ ] Queue management

**Week 3 Day 6-7: Testing & Edge Cases**
- [ ] Unit tests (30+ tests)
- [ ] Quality constraint tests
- [ ] Termination tests
- [ ] Edge case handling

---

## Key Tests

```csharp
[Fact]
public void Refine_EnforcesMinimumAngle()
{
    var cdt = new ConstrainedDelaunayTriangulation<Point2<double>>();
    // ... insert points ...

    var params = new RefinementParameters<double>()
        .WithAngleLimit(AngleLimit.FromDegrees(20.0));

    cdt.Refine(params);

    // Verify all triangles have min angle >= 20°
    foreach (var face in cdt.InnerFaces())
    {
        var angles = ComputeAngles(face);
        var minAngle = angles.Min();

        Assert.True(minAngle >= 19.9, // Allow small tolerance
            $"Triangle has angle {minAngle}° < 20°");
    }
}

[Fact]
public void Refine_EnforcesMaximumArea()
{
    var cdt = new ConstrainedDelaunayTriangulation<Point2<double>>();
    // ... insert points ...

    var params = new RefinementParameters<double>()
        .WithMaxAllowedArea(0.5);

    cdt.Refine(params);

    // Verify all triangles have area <= 0.5
    foreach (var face in cdt.InnerFaces())
    {
        var area = ComputeArea(face);
        Assert.True(area <= 0.51, // Allow small tolerance
            $"Triangle has area {area} > 0.5");
    }
}
```

---

## Success Criteria

✅ Can enforce minimum angle constraints
✅ Can enforce maximum area constraints
✅ Steiner points inserted correctly
✅ Algorithm terminates
✅ Performance: 10,000 points refined in <5 seconds
✅ All tests passing (35+ tests)

---

**END OF RFC-004**
