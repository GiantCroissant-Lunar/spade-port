using System.Collections.Generic;
using Spade.Primitives;

namespace Spade.Advanced.Power;

public sealed class PowerCell
{
    private readonly List<Point2<double>> _polygon;
    private readonly List<int> _neighborSiteIndices;

    public int SiteIndex { get; }

    public WeightedPoint Site { get; }

    public IReadOnlyList<Point2<double>> Polygon => _polygon;

    public IReadOnlyList<int> NeighborSiteIndices => _neighborSiteIndices;

    internal PowerCell(
        int siteIndex,
        WeightedPoint site,
        List<Point2<double>> polygon,
        List<int> neighborSiteIndices)
    {
        SiteIndex = siteIndex;
        Site = site;
        _polygon = polygon;
        _neighborSiteIndices = neighborSiteIndices;
    }
}
