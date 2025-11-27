using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Spade.Primitives;

namespace Spade.Advanced.Interpolation;

public static class DiscreteGridNaturalNeighbor3D
{
    public static double[,,] InterpolateToGrid(
        IReadOnlyList<Point3<double>> samplePoints,
        IReadOnlyList<double> sampleValues,
        int nx,
        int ny,
        int nz,
        Point3<double> min,
        Point3<double> max,
        double outsideValue = double.NaN)
    {
        if (samplePoints is null) throw new ArgumentNullException(nameof(samplePoints));
        if (sampleValues is null) throw new ArgumentNullException(nameof(sampleValues));
        if (nx <= 0) throw new ArgumentOutOfRangeException(nameof(nx));
        if (ny <= 0) throw new ArgumentOutOfRangeException(nameof(ny));
        if (nz <= 0) throw new ArgumentOutOfRangeException(nameof(nz));
        if (samplePoints.Count != sampleValues.Count)
            throw new ArgumentException("samplePoints and sampleValues must have the same length.", nameof(sampleValues));
        if (samplePoints.Count == 0)
            throw new ArgumentException("samplePoints must not be empty.", nameof(samplePoints));
        if (nx > 1 && max.X <= min.X)
            throw new ArgumentException("max.X must be greater than min.X when nx > 1.", nameof(max));
        if (ny > 1 && max.Y <= min.Y)
            throw new ArgumentException("max.Y must be greater than min.Y when ny > 1.", nameof(max));
        if (nz > 1 && max.Z <= min.Z)
            throw new ArgumentException("max.Z must be greater than min.Z when nz > 1.", nameof(max));

        var sampleCount = samplePoints.Count;
        var sampleIndexX = new double[sampleCount];
        var sampleIndexY = new double[sampleCount];
        var sampleIndexZ = new double[sampleCount];
        var sampleValue = new double[sampleCount];

        double sx = nx == 1 ? 0.0 : (nx - 1) / (max.X - min.X);
        double sy = ny == 1 ? 0.0 : (ny - 1) / (max.Y - min.Y);
        double sz = nz == 1 ? 0.0 : (nz - 1) / (max.Z - min.Z);

        for (int i = 0; i < sampleCount; i++)
        {
            var p = samplePoints[i];
            sampleIndexX[i] = nx == 1 ? 0.0 : (p.X - min.X) * sx;
            sampleIndexY[i] = ny == 1 ? 0.0 : (p.Y - min.Y) * sy;
            sampleIndexZ[i] = nz == 1 ? 0.0 : (p.Z - min.Z) * sz;
            sampleValue[i] = sampleValues[i];
        }

        var indices = new int[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            indices[i] = i;
        }

        var nodes = new KdNode3D[sampleCount];
        var comparerX = new XComparer3D(sampleIndexX);
        var comparerY = new YComparer3D(sampleIndexY);
        var comparerZ = new ZComparer3D(sampleIndexZ);
        var root = BuildKdTree3D(sampleIndexX, sampleIndexY, sampleIndexZ, indices, 0, sampleCount, 0, nodes, comparerX, comparerY, comparerZ);

        var nearestIndex = new int[nz, ny, nx];
        var nearestDistSq = new double[nz, ny, nx];

        Parallel.For(0, nz, iz =>
        {
            for (int iy = 0; iy < ny; iy++)
            {
                for (int ix = 0; ix < nx; ix++)
                {
                    var bestDist = double.PositiveInfinity;
                    var bestIdx = -1;

                    FindNearest3D(nodes, root, sampleIndexX, sampleIndexY, sampleIndexZ, ix, iy, iz, ref bestDist, ref bestIdx);

                    nearestIndex[iz, iy, ix] = bestIdx;
                    nearestDistSq[iz, iy, ix] = bestDist;
                }
            }
        });

        var grid = new double[nz, ny, nx];
        var counts = new int[nz, ny, nx];

        for (int iz = 0; iz < nz; iz++)
        {
            for (int iy = 0; iy < ny; iy++)
            {
                for (int ix = 0; ix < nx; ix++)
                {
                    var bestIdx = nearestIndex[iz, iy, ix];
                    if (bestIdx < 0)
                    {
                        continue;
                    }

                    var dQuery = nearestDistSq[iz, iy, ix];
                    var radius = (int)Math.Ceiling(Math.Sqrt(dQuery));

                    int minZ = iz - radius;
                    if (minZ < 0) minZ = 0;
                    int maxZ = iz + radius;
                    if (maxZ >= nz) maxZ = nz - 1;

                    int minY = iy - radius;
                    if (minY < 0) minY = 0;
                    int maxY = iy + radius;
                    if (maxY >= ny) maxY = ny - 1;

                    int minX = ix - radius;
                    if (minX < 0) minX = 0;
                    int maxX = ix + radius;
                    if (maxX >= nx) maxX = nx - 1;

                    var v = sampleValue[bestIdx];

                    for (int kz = minZ; kz <= maxZ; kz++)
                    {
                        var dz = iz - kz;
                        var dz2 = dz * dz;
                        for (int jy = minY; jy <= maxY; jy++)
                        {
                            var dy = iy - jy;
                            var dy2 = dy * dy;
                            for (int jx = minX; jx <= maxX; jx++)
                            {
                                var dx = ix - jx;
                                var d2 = dx * dx + dy2 + dz2;

                                if (d2 == 0.0 || d2 < dQuery)
                                {
                                    grid[kz, jy, jx] += v;
                                    counts[kz, jy, jx]++;
                                }
                            }
                        }
                    }
                }
            }
        }

        for (int iz = 0; iz < nz; iz++)
        {
            for (int iy = 0; iy < ny; iy++)
            {
                for (int ix = 0; ix < nx; ix++)
                {
                    var c = counts[iz, iy, ix];
                    if (c > 0)
                    {
                        grid[iz, iy, ix] /= c;
                    }
                    else
                    {
                        grid[iz, iy, ix] = outsideValue;
                    }
                }
            }
        }

        return grid;
    }

    private struct KdNode3D
    {
        public int SampleIndex;
        public int Left;
        public int Right;
        public int Axis;
    }

    private sealed class XComparer3D : IComparer<int>
    {
        private readonly double[] _xs;

        public XComparer3D(double[] xs)
        {
            _xs = xs;
        }

        public int Compare(int a, int b)
        {
            return _xs[a].CompareTo(_xs[b]);
        }
    }

    private sealed class YComparer3D : IComparer<int>
    {
        private readonly double[] _ys;

        public YComparer3D(double[] ys)
        {
            _ys = ys;
        }

        public int Compare(int a, int b)
        {
            return _ys[a].CompareTo(_ys[b]);
        }
    }

    private sealed class ZComparer3D : IComparer<int>
    {
        private readonly double[] _zs;

        public ZComparer3D(double[] zs)
        {
            _zs = zs;
        }

        public int Compare(int a, int b)
        {
            return _zs[a].CompareTo(_zs[b]);
        }
    }

    private static int BuildKdTree3D(
        double[] xs,
        double[] ys,
        double[] zs,
        int[] indices,
        int start,
        int end,
        int depth,
        KdNode3D[] nodes,
        IComparer<int> comparerX,
        IComparer<int> comparerY,
        IComparer<int> comparerZ)
    {
        if (start >= end)
        {
            return -1;
        }

        var axis = depth % 3;
        var length = end - start;
        switch (axis)
        {
            case 0:
                Array.Sort(indices, start, length, comparerX);
                break;
            case 1:
                Array.Sort(indices, start, length, comparerY);
                break;
            default:
                Array.Sort(indices, start, length, comparerZ);
                break;
        }

        var mid = start + (length >> 1);
        var sampleIndex = indices[mid];

        var left = BuildKdTree3D(xs, ys, zs, indices, start, mid, depth + 1, nodes, comparerX, comparerY, comparerZ);
        var right = BuildKdTree3D(xs, ys, zs, indices, mid + 1, end, depth + 1, nodes, comparerX, comparerY, comparerZ);

        nodes[sampleIndex] = new KdNode3D
        {
            SampleIndex = sampleIndex,
            Left = left,
            Right = right,
            Axis = axis
        };

        return sampleIndex;
    }

    private static void FindNearest3D(
        KdNode3D[] nodes,
        int nodeIndex,
        double[] xs,
        double[] ys,
        double[] zs,
        double qx,
        double qy,
        double qz,
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
        var dz = qz - zs[sampleIndex];
        var d2 = dx * dx + dy * dy + dz * dz;
        if (d2 < bestDistSq)
        {
            bestDistSq = d2;
            bestIndex = sampleIndex;
        }

        double queryCoord;
        double splitCoord;

        switch (node.Axis)
        {
            case 0:
                queryCoord = qx;
                splitCoord = xs[sampleIndex];
                break;
            case 1:
                queryCoord = qy;
                splitCoord = ys[sampleIndex];
                break;
            default:
                queryCoord = qz;
                splitCoord = zs[sampleIndex];
                break;
        }

        var goLeftFirst = queryCoord < splitCoord;
        var nearChild = goLeftFirst ? node.Left : node.Right;
        var farChild = goLeftFirst ? node.Right : node.Left;

        FindNearest3D(nodes, nearChild, xs, ys, zs, qx, qy, qz, ref bestDistSq, ref bestIndex);

        var diff = queryCoord - splitCoord;
        var diff2 = diff * diff;
        if (diff2 < bestDistSq)
        {
            FindNearest3D(nodes, farChild, xs, ys, zs, qx, qy, qz, ref bestDistSq, ref bestIndex);
        }
    }
}
