using System;
using Spade.Primitives;

namespace Spade.Advanced.Power;

/// <summary>
/// Helper methods for power distance computations used in weighted Delaunay and power diagrams.
/// </summary>
public static class PowerGeometry
{
    /// <summary>
    /// Computes the power distance &pi;(x) = |x - p|^2 - w for a weighted site (p, w).
    /// </summary>
    public static double PowerDistance(WeightedPoint site, Point2<double> point)
    {
        var dx = point.X - site.Position.X;
        var dy = point.Y - site.Position.Y;
        var r2 = dx * dx + dy * dy;
        return r2 - site.Weight;
    }

    /// <summary>
    /// Compares power distances from two weighted sites to a query point.
    /// Returns -1 if a is closer, +1 if b is closer, 0 if approximately equal.
    /// </summary>
    public static int ComparePowerDistance(WeightedPoint a, WeightedPoint b, Point2<double> point, double epsilon = 1e-9)
    {
        var da = PowerDistance(a, point);
        var db = PowerDistance(b, point);
        var diff = da - db;

        if (Math.Abs(diff) <= epsilon) return 0;
        return diff < 0 ? -1 : 1;
    }
}
