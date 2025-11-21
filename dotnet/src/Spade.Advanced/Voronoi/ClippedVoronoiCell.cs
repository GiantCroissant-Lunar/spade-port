using System.Collections.Generic;
using Spade.Primitives;

namespace Spade.Advanced.Voronoi;

public sealed class ClippedVoronoiCell<TVertex>
{
    private readonly List<Point2<double>> _polygon;

    public TVertex Generator { get; }

    public IReadOnlyList<Point2<double>> Polygon => _polygon;

    public bool IsClipped { get; }

    internal ClippedVoronoiCell(TVertex generator, List<Point2<double>> polygon, bool isClipped)
    {
        Generator = generator;
        _polygon = polygon;
        IsClipped = isClipped;
    }
}
