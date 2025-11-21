using System;
using System.Collections.Generic;
using Spade.Primitives;

namespace Spade.Advanced.Power;

/// <summary>
/// Query helpers for power diagrams, such as finding the nearest weighted site
/// in power-distance sense.
/// </summary>
public static class PowerDiagramQueries
{
    /// <summary>
    /// Returns the index of the site with minimal power distance to the given point.
    /// </summary>
    /// <exception cref="ArgumentNullException">If <paramref name="sites" /> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="sites" /> is empty.</exception>
    public static int FindNearestSiteIndex(IReadOnlyList<WeightedPoint> sites, Point2<double> point)
    {
        if (sites is null) throw new ArgumentNullException(nameof(sites));
        if (sites.Count == 0) throw new ArgumentException("Sequence of sites must not be empty.", nameof(sites));

        var bestIndex = 0;
        var bestValue = PowerGeometry.PowerDistance(sites[0], point);

        for (var i = 1; i < sites.Count; i++)
        {
            var value = PowerGeometry.PowerDistance(sites[i], point);
            if (value < bestValue)
            {
                bestValue = value;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    /// <summary>
    /// Returns the site with minimal power distance to the given point.
    /// </summary>
    /// <exception cref="ArgumentNullException">If <paramref name="sites" /> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="sites" /> is empty.</exception>
    public static WeightedPoint FindNearestSite(IReadOnlyList<WeightedPoint> sites, Point2<double> point)
    {
        var index = FindNearestSiteIndex(sites, point);
        return sites[index];
    }
}
