using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Spade.Primitives;

namespace Spade.Advanced.Interpolation;

public static class DiscreteGridNaturalNeighbor2D
{
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
            throw new ArgumentException("samplePoints and sampleValues must have the same length.", nameof(sampleValues));
        if (samplePoints.Count == 0)
            throw new ArgumentException("samplePoints must not be empty.", nameof(samplePoints));
        if (width > 1 && max.X <= min.X)
            throw new ArgumentException("max.X must be greater than min.X when width > 1.", nameof(max));
        if (height > 1 && max.Y <= min.Y)
            throw new ArgumentException("max.Y must be greater than min.Y when height > 1.", nameof(max));

        var sampleCount = samplePoints.Count;
        var sampleIndexX = new double[sampleCount];
        var sampleIndexY = new double[sampleCount];
        var sampleValue = new double[sampleCount];

        double sx = width == 1 ? 0.0 : (width - 1) / (max.X - min.X);
        double sy = height == 1 ? 0.0 : (height - 1) / (max.Y - min.Y);

        for (int i = 0; i < sampleCount; i++)
        {
            var p = samplePoints[i];
            sampleIndexX[i] = width == 1 ? 0.0 : (p.X - min.X) * sx;
            sampleIndexY[i] = height == 1 ? 0.0 : (p.Y - min.Y) * sy;
            sampleValue[i] = sampleValues[i];
        }
        var indices = new int[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            indices[i] = i;
        }

        var nodes = new KdNode[sampleCount];
        var comparerX = new XComparer(sampleIndexX);
        var comparerY = new YComparer(sampleIndexY);
        var root = BuildKdTree(sampleIndexX, sampleIndexY, indices, 0, sampleCount, 0, nodes, comparerX, comparerY);

        var nearestIndex = new int[height, width];
        var nearestDistSq = new double[height, width];

        Parallel.For(0, height, iy =>
        {
            for (int ix = 0; ix < width; ix++)
            {
                var bestDist = double.PositiveInfinity;
                var bestIdx = -1;

                FindNearest(nodes, root, sampleIndexX, sampleIndexY, ix, iy, ref bestDist, ref bestIdx);

                nearestIndex[iy, ix] = bestIdx;
                nearestDistSq[iy, ix] = bestDist;
            }
        });

        var grid = new double[height, width];
        var counts = new int[height, width];

        for (int iy = 0; iy < height; iy++)
        {
            for (int ix = 0; ix < width; ix++)
            {
                var bestIdx = nearestIndex[iy, ix];
                if (bestIdx < 0)
                {
                    continue;
                }

                var dQuery = nearestDistSq[iy, ix];
                var radius = (int)Math.Ceiling(Math.Sqrt(dQuery));

                int minY = iy - radius;
                if (minY < 0) minY = 0;
                int maxY = iy + radius;
                if (maxY >= height) maxY = height - 1;

                int minX = ix - radius;
                if (minX < 0) minX = 0;
                int maxX = ix + radius;
                if (maxX >= width) maxX = width - 1;

                var v = sampleValue[bestIdx];

                for (int jy = minY; jy <= maxY; jy++)
                {
                    var dy = iy - jy;
                    var dy2 = dy * dy;
                    for (int jx = minX; jx <= maxX; jx++)
                    {
                        var dx = ix - jx;
                        var d2 = dx * dx + dy2;

                        if (d2 == 0.0 || d2 < dQuery)
                        {
                            grid[jy, jx] += v;
                            counts[jy, jx]++;
                        }
                    }
                }
            }
        }

        for (int iy = 0; iy < height; iy++)
        {
            for (int ix = 0; ix < width; ix++)
            {
                var c = counts[iy, ix];
                if (c > 0)
                {
                    grid[iy, ix] /= c;
                }
                else
                {
                    grid[iy, ix] = outsideValue;
                }
            }
        }

        return grid;
    }

    private struct KdNode
    {
        public int SampleIndex;
        public int Left;
        public int Right;
        public int Axis;
    }

    private sealed class XComparer : IComparer<int>
    {
        private readonly double[] _xs;

        public XComparer(double[] xs)
        {
            _xs = xs;
        }

        public int Compare(int a, int b)
        {
            return _xs[a].CompareTo(_xs[b]);
        }
    }

    private sealed class YComparer : IComparer<int>
    {
        private readonly double[] _ys;

        public YComparer(double[] ys)
        {
            _ys = ys;
        }

        public int Compare(int a, int b)
        {
            return _ys[a].CompareTo(_ys[b]);
        }
    }

    private static int BuildKdTree(
        double[] xs,
        double[] ys,
        int[] indices,
        int start,
        int end,
        int depth,
        KdNode[] nodes,
        IComparer<int> comparerX,
        IComparer<int> comparerY)
    {
        if (start >= end)
        {
            return -1;
        }

        var axis = depth & 1;
        var length = end - start;
        if (axis == 0)
        {
            Array.Sort(indices, start, length, comparerX);
        }
        else
        {
            Array.Sort(indices, start, length, comparerY);
        }

        var mid = start + (length >> 1);
        var sampleIndex = indices[mid];

        var left = BuildKdTree(xs, ys, indices, start, mid, depth + 1, nodes, comparerX, comparerY);
        var right = BuildKdTree(xs, ys, indices, mid + 1, end, depth + 1, nodes, comparerX, comparerY);

        nodes[sampleIndex] = new KdNode
        {
            SampleIndex = sampleIndex,
            Left = left,
            Right = right,
            Axis = axis
        };

        return sampleIndex;
    }

    private static void FindNearest(
        KdNode[] nodes,
        int nodeIndex,
        double[] xs,
        double[] ys,
        double qx,
        double qy,
        ref double bestDistSq,
        ref int bestIndex)
    {
        if (nodeIndex < 0)
        {
            return;
        }

        var node = nodes[nodeIndex];
        var sampleIndex = node.SampleIndex;

        var dx = qx - xs[sampleIndex];
        var dy = qy - ys[sampleIndex];
        var d2 = dx * dx + dy * dy;
        if (d2 < bestDistSq)
        {
            bestDistSq = d2;
            bestIndex = sampleIndex;
        }

        double queryCoord;
        double splitCoord;

        if (node.Axis == 0)
        {
            queryCoord = qx;
            splitCoord = xs[sampleIndex];
        }
        else
        {
            queryCoord = qy;
            splitCoord = ys[sampleIndex];
        }

        var goLeftFirst = queryCoord < splitCoord;
        var nearChild = goLeftFirst ? node.Left : node.Right;
        var farChild = goLeftFirst ? node.Right : node.Left;

        FindNearest(nodes, nearChild, xs, ys, qx, qy, ref bestDistSq, ref bestIndex);

        var diff = queryCoord - splitCoord;
        var diff2 = diff * diff;
        if (diff2 < bestDistSq)
        {
            FindNearest(nodes, farChild, xs, ys, qx, qy, ref bestDistSq, ref bestIndex);
        }
    }
}
