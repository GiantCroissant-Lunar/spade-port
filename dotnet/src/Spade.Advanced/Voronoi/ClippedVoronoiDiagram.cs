using System.Collections.Generic;

namespace Spade.Advanced.Voronoi;

public sealed class ClippedVoronoiDiagram<TVertex>
{
    private readonly List<ClippedVoronoiCell<TVertex>> _cells;

    public ClipPolygon Domain { get; }

    public IReadOnlyList<ClippedVoronoiCell<TVertex>> Cells => _cells;

    internal ClippedVoronoiDiagram(ClipPolygon domain, List<ClippedVoronoiCell<TVertex>> cells)
    {
        Domain = domain;
        _cells = cells;
    }
}
