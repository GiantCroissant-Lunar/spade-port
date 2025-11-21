using Spade.Primitives;

namespace Spade.Advanced.Power;

/// <summary>
/// Represents a weighted site for power diagram constructions.
/// </summary>
public readonly struct WeightedPoint
{
    public Point2<double> Position { get; }
    public double Weight { get; }

    public WeightedPoint(Point2<double> position, double weight)
    {
        Position = position;
        Weight = weight;
    }
}
