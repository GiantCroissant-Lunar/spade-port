using System.Collections.Generic;
using Spade.Primitives;

namespace Spade.Advanced.Voronoi;

/// <summary>
/// Represents a single Voronoi cell that has been clipped to a bounding domain polygon.
/// </summary>
/// <typeparam name="TVertex">The type of vertex data stored at the generator point.</typeparam>
public sealed class ClippedVoronoiCell<TVertex>
{
    private readonly List<Point2<double>> _polygon;

    /// <summary>
    /// Gets the generator (site) vertex data that defines this Voronoi cell.
    /// </summary>
    public TVertex Generator { get; }

    /// <summary>
    /// Gets the original index of the generator vertex in the triangulation.
    /// This index corresponds to the order in which the vertex was inserted into the triangulation
    /// and can be used to map cells back to the original input site array.
    /// </summary>
    public int GeneratorIndex { get; }

    /// <summary>
    /// Gets the polygon vertices that define the boundary of this clipped cell.
    /// </summary>
    public IReadOnlyList<Point2<double>> Polygon => _polygon;

    /// <summary>
    /// Gets a value indicating whether this cell was clipped by the bounding domain.
    /// </summary>
    public bool IsClipped { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClippedVoronoiCell{TVertex}"/> class.
    /// </summary>
    /// <param name="generator">The generator vertex data.</param>
    /// <param name="generatorIndex">The original index of the generator in the triangulation.</param>
    /// <param name="polygon">The polygon vertices defining the cell boundary.</param>
    /// <param name="isClipped">Whether the cell was clipped by the domain boundary.</param>
    internal ClippedVoronoiCell(TVertex generator, int generatorIndex, List<Point2<double>> polygon, bool isClipped)
    {
        Generator = generator;
        GeneratorIndex = generatorIndex;
        _polygon = polygon;
        IsClipped = isClipped;
    }
}
