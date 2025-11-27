using System.Collections.Generic;
using Spade.Primitives;

namespace Spade.Advanced.Interpolation;

public static class NaturalNeighborGrid2D
{
    public static double[,] Exact(
        IReadOnlyList<Point2<double>> samplePoints,
        IReadOnlyList<double> sampleValues,
        int width,
        int height,
        Point2<double> min,
        Point2<double> max,
        double outsideValue = double.NaN)
    {
        return GridNaturalNeighbor2D.InterpolateToGrid(
            samplePoints,
            sampleValues,
            width,
            height,
            min,
            max,
            outsideValue);
    }

    public static double[,] Discrete(
        IReadOnlyList<Point2<double>> samplePoints,
        IReadOnlyList<double> sampleValues,
        int width,
        int height,
        Point2<double> min,
        Point2<double> max,
        double outsideValue = double.NaN)
    {
        return DiscreteGridNaturalNeighbor2D.InterpolateToGrid(
            samplePoints,
            sampleValues,
            width,
            height,
            min,
            max,
            outsideValue);
    }
}
