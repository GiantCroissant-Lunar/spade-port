using System;

namespace Spade.Refinement;

/// <summary>
/// Represents a minimum angle constraint used during mesh refinement.
/// </summary>
public readonly struct AngleLimit
{
    private readonly double _radians;
    private readonly double _radiusToShortestEdgeLimit;

    public AngleLimit(double radians)
    {
        _radians = radians;
        _radiusToShortestEdgeLimit = ComputeRadiusToShortestEdgeLimit(radians);
    }

    public static AngleLimit FromDegrees(double degrees) =>
        new AngleLimit(degrees * Math.PI / 180.0);

    public static AngleLimit FromRadians(double radians) =>
        new AngleLimit(radians);

    public static AngleLimit FromRadiusToShortestEdgeRatio(double ratio)
    {
        if (double.IsPositiveInfinity(ratio))
        {
            return new AngleLimit(0.0);
        }

        var radians = Math.Asin(0.5 / ratio);
        return new AngleLimit(radians);
    }

    public double Degrees => _radians * 180.0 / Math.PI;

    public double Radians => _radians;

    public double RadiusToShortestEdgeLimit => _radiusToShortestEdgeLimit;

    private static double ComputeRadiusToShortestEdgeLimit(double radians)
    {
        var sin = Math.Sin(radians);
        if (sin == 0.0)
        {
            return double.PositiveInfinity;
        }

        return 0.5 / sin;
    }
}
