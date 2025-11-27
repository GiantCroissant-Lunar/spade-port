using System;
using System.Collections.Generic;
using Spade;
using Spade.Primitives;

namespace Spade.Advanced.Interpolation;

public static class GridNaturalNeighbor2D
{
    private readonly struct PointWithValue : IHasPosition<double>
    {
        public Point2<double> Position { get; }
        public double Value { get; }

        public PointWithValue(Point2<double> position, double value)
        {
            Position = position;
            Value = value;
        }
    }

    public static double[,] InterpolateToGrid(
        IReadOnlyList<Point2<double>> samplePoints,
        IReadOnlyList<double> sampleValues,
        int width,
        int height,
        Point2<double> min,
        Point2<double> max,
        double outsideValue = double.NaN)
    {
        if (samplePoints is null) throw new ArgumentNullException(nameof(samplePoints));
        if (sampleValues is null) throw new ArgumentNullException(nameof(sampleValues));
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

        if (samplePoints.Count != sampleValues.Count)
        {
            throw new ArgumentException("samplePoints and sampleValues must have the same length.", nameof(sampleValues));
        }

        if (samplePoints.Count == 0)
        {
            throw new ArgumentException("samplePoints must not be empty.", nameof(samplePoints));
        }

        var triangulation = new DelaunayTriangulation<PointWithValue, int, int, int, LastUsedVertexHintGenerator<double>>();
        for (int i = 0; i < samplePoints.Count; i++)
        {
            var p = samplePoints[i];
            var v = sampleValues[i];
            triangulation.Insert(new PointWithValue(p, v));
        }

        var interpolator = triangulation.NaturalNeighbor();

        var grid = new double[height, width];

        double dx;
        double dy;

        if (width == 1)
        {
            dx = 0.0;
        }
        else
        {
            dx = (max.X - min.X) / (width - 1);
        }

        if (height == 1)
        {
            dy = 0.0;
        }
        else
        {
            dy = (max.Y - min.Y) / (height - 1);
        }

        for (int iy = 0; iy < height; iy++)
        {
            var y = height == 1 ? 0.5 * (min.Y + max.Y) : min.Y + iy * dy;
            for (int ix = 0; ix < width; ix++)
            {
                var x = width == 1 ? 0.5 * (min.X + max.X) : min.X + ix * dx;
                var position = new Point2<double>(x, y);
                var value = interpolator.Interpolate(v => ((PointWithValue)v.Data).Value, position);
                grid[iy, ix] = value ?? outsideValue;
            }
        }

        return grid;
    }
}
