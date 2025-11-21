using System.Collections.Generic;

namespace Spade.Advanced.Power;

public sealed class PowerDiagram
{
    private readonly List<WeightedPoint> _sites;
    private readonly List<PowerCell> _cells;

    public IReadOnlyList<WeightedPoint> Sites => _sites;

    public IReadOnlyList<PowerCell> Cells => _cells;

    internal PowerDiagram(List<WeightedPoint> sites, List<PowerCell> cells)
    {
        _sites = sites;
        _cells = cells;
    }
}
