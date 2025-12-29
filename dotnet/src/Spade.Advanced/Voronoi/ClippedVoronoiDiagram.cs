using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Spade.Advanced.Voronoi;

/// <summary>
/// Represents a complete clipped Voronoi diagram containing all cells for a triangulation
/// that have been intersected with a bounding domain polygon.
/// </summary>
/// <typeparam name="TVertex">The type of vertex data stored at generator points.</typeparam>
public sealed class ClippedVoronoiDiagram<TVertex>
{
    private readonly List<ClippedVoronoiCell<TVertex>> _cells;
    private readonly List<int> _degenerateCells;
    private readonly List<int> _outsideDomain;
    private readonly Dictionary<int, int> _indexToCell;

    /// <summary>
    /// Gets the bounding domain polygon used for clipping.
    /// </summary>
    public ClipPolygon Domain { get; }

    /// <summary>
    /// Gets the collection of valid clipped cells, ordered by ascending GeneratorIndex.
    /// </summary>
    public IReadOnlyList<ClippedVoronoiCell<TVertex>> Cells => _cells;

    /// <summary>
    /// Gets the collection of generator indices whose cells became degenerate
    /// (fewer than 3 vertices after clipping).
    /// </summary>
    public IReadOnlyList<int> DegenerateCells => _degenerateCells;

    /// <summary>
    /// Gets the collection of generator indices that lie entirely outside the clipping domain.
    /// </summary>
    public IReadOnlyList<int> OutsideDomain => _outsideDomain;

    /// <summary>
    /// Gets the cell for the specified generator index, or null if no valid cell exists.
    /// </summary>
    /// <param name="generatorIndex">The generator index to look up.</param>
    /// <returns>The cell if found and valid; otherwise, null.</returns>
    public ClippedVoronoiCell<TVertex>? this[int generatorIndex]
    {
        get
        {
            if (_indexToCell.TryGetValue(generatorIndex, out var cellIndex))
            {
                return _cells[cellIndex];
            }
            return null;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClippedVoronoiDiagram{TVertex}"/> class.
    /// </summary>
    /// <param name="domain">The bounding domain polygon.</param>
    /// <param name="cells">The list of valid clipped cells.</param>
    /// <param name="degenerateCells">The list of generator indices with degenerate cells.</param>
    /// <param name="outsideDomain">The list of generator indices outside the domain.</param>
    internal ClippedVoronoiDiagram(
        ClipPolygon domain,
        List<ClippedVoronoiCell<TVertex>> cells,
        List<int> degenerateCells,
        List<int> outsideDomain)
    {
        Domain = domain;
        _cells = cells;
        _degenerateCells = degenerateCells;
        _outsideDomain = outsideDomain;

        // Build lookup dictionary for O(1) cell access by generator index
        _indexToCell = new Dictionary<int, int>(cells.Count);
        for (var i = 0; i < cells.Count; i++)
        {
            _indexToCell[cells[i].GeneratorIndex] = i;
        }
    }

    /// <summary>
    /// Attempts to get the cell for the specified generator index.
    /// </summary>
    /// <param name="generatorIndex">The generator index to look up.</param>
    /// <param name="cell">When this method returns, contains the cell if found; otherwise, null.</param>
    /// <returns>true if a valid cell exists for the generator index; otherwise, false.</returns>
    public bool TryGetCell(int generatorIndex, [NotNullWhen(true)] out ClippedVoronoiCell<TVertex>? cell)
    {
        if (_indexToCell.TryGetValue(generatorIndex, out var cellIndex))
        {
            cell = _cells[cellIndex];
            return true;
        }
        cell = null;
        return false;
    }

    /// <summary>
    /// Determines whether a valid (non-degenerate) cell exists for the specified generator index.
    /// </summary>
    /// <param name="generatorIndex">The generator index to check.</param>
    /// <returns>true if a valid cell exists; otherwise, false.</returns>
    public bool HasValidCell(int generatorIndex)
    {
        return _indexToCell.ContainsKey(generatorIndex);
    }
}
