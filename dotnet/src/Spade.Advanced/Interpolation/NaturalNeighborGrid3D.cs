using System.Collections.Generic;
using Spade.Primitives;

namespace Spade.Advanced.Interpolation;

public static class NaturalNeighborGrid3D
{
    public static double[,,] Discrete(
        IReadOnlyList<Point3<double>> samplePoints,
        IReadOnlyList<double> sampleValues,
        int nx,
        int ny,
        int nz,
        Point3<double> min,
        Point3<double> max,
        double outsideValue = double.NaN)
    {
        return DiscreteGridNaturalNeighbor3D.InterpolateToGrid(
            samplePoints,
            sampleValues,
            nx,
            ny,
            nz,
            min,
            max,
            outsideValue);
    }
}
