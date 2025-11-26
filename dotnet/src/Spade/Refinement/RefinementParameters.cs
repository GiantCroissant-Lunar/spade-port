namespace Spade.Refinement;

/// <summary>
/// Parameters controlling mesh refinement.
/// This initial version focuses on maximum triangle area; angle-based
/// refinement can be added incrementally.
/// </summary>
public sealed class RefinementParameters
{
    /// <summary>
    /// Minimum allowed angle in the mesh. Currently not enforced by the
    /// area-only refinement implementation, but included for API parity
    /// with RFC-004.
    /// </summary>
    public AngleLimit AngleLimit { get; set; } = AngleLimit.FromDegrees(30.0);

    /// <summary>
    /// Whether minimum-angle based refinement is enabled. This is toggled
    /// automatically by <see cref="WithAngleLimit"/>.
    /// </summary>
    public bool EnableAngleRefinement { get; set; }

    /// <summary>
    /// Maximum allowed triangle area. Triangles with area strictly greater
    /// than this value may be split by inserting Steiner points.
    /// </summary>
    public double? MaxAllowedArea { get; set; }

    /// <summary>
    /// Optional lower bound on triangle area; currently unused.
    /// </summary>
    public double? MinRequiredArea { get; set; }

    /// <summary>
    /// Whether constraint edges must be preserved. The current
    /// refinement implementation inserts Steiner points only in triangle
    /// interiors, so constraint edges are preserved implicitly.
    /// </summary>
    public bool KeepConstraintEdges { get; set; } = true;

    /// <summary>
    /// Whether to exclude the outer face from refinement. This implementation
    /// always refines only inner faces, so this is effectively always true.
    /// </summary>
    public bool ExcludeOuterFaces { get; set; } = true;

    /// <summary>
    /// Maximum number of additional vertices that refinement is allowed to add.
    /// Used as a safety cap to prevent non-termination.
    /// </summary>
    public int MaxAdditionalVertices { get; set; } = 10_000;

    public RefinementParameters WithAngleLimit(AngleLimit limit)
    {
        AngleLimit = limit;
        EnableAngleRefinement = true;
        return this;
    }

    public RefinementParameters WithMaxAllowedArea(double area)
    {
        MaxAllowedArea = area;
        return this;
    }
}
