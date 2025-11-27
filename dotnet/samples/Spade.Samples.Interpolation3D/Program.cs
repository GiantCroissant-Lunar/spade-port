using System.Diagnostics;
using Spade.Advanced.Interpolation;
using Spade.Primitives;

Console.WriteLine("Spade.NET sample - 3D discrete natural neighbor grid interpolation\n");

static double TrueFunction3D(Point3<double> p) => Math.Sin(p.X) + Math.Cos(p.Y) + 0.5 * p.Z;

static double ComputeRmse3D(double[,,] grid, Point3<double> min, Point3<double> max)
{
    var nz = grid.GetLength(0);
    var ny = grid.GetLength(1);
    var nx = grid.GetLength(2);

    var dx = nx == 1 ? 0.0 : (max.X - min.X) / (nx - 1);
    var dy = ny == 1 ? 0.0 : (max.Y - min.Y) / (ny - 1);
    var dz = nz == 1 ? 0.0 : (max.Z - min.Z) / (nz - 1);

    double sumSq = 0.0;
    long count = 0;

    for (int iz = 0; iz < nz; iz++)
    {
        var z = nz == 1 ? 0.5 * (min.Z + max.Z) : min.Z + iz * dz;
        for (int iy = 0; iy < ny; iy++)
        {
            var y = ny == 1 ? 0.5 * (min.Y + max.Y) : min.Y + iy * dy;
            for (int ix = 0; ix < nx; ix++)
            {
                var x = nx == 1 ? 0.5 * (min.X + max.X) : min.X + ix * dx;
                var v = grid[iz, iy, ix];
                if (double.IsNaN(v) || double.IsInfinity(v))
                {
                    continue;
                }

                var truth = TrueFunction3D(new Point3<double>(x, y, z));
                var diff = v - truth;
                sumSq += diff * diff;
                count++;
            }
        }
    }

    return count > 0 ? Math.Sqrt(sumSq / count) : double.NaN;
}

static void Run3DGridBenchmark()
{
    Console.WriteLine("3D grid interpolation benchmark (discrete)\n");

    const int samplePerAxis = 21;
    const double minCoord = -2.0;
    const double maxCoord = 2.0;

    var samplePoints = new List<Point3<double>>();
    var sampleValues = new List<double>();

    for (int iz = 0; iz < samplePerAxis; iz++)
    {
        var tz = samplePerAxis == 1 ? 0.5 : iz / (double)(samplePerAxis - 1);
        var z = minCoord + tz * (maxCoord - minCoord);
        for (int iy = 0; iy < samplePerAxis; iy++)
        {
            var ty = samplePerAxis == 1 ? 0.5 : iy / (double)(samplePerAxis - 1);
            var y = minCoord + ty * (maxCoord - minCoord);
            for (int ix = 0; ix < samplePerAxis; ix++)
            {
                var tx = samplePerAxis == 1 ? 0.5 : ix / (double)(samplePerAxis - 1);
                var x = minCoord + tx * (maxCoord - minCoord);

                var p = new Point3<double>(x, y, z);
                samplePoints.Add(p);
                sampleValues.Add(TrueFunction3D(p));
            }
        }
    }

    var min = new Point3<double>(minCoord, minCoord, minCoord);
    var max = new Point3<double>(maxCoord, maxCoord, maxCoord);

    const int nx = 64;
    const int ny = 64;
    const int nz = 64;

    var sw = Stopwatch.StartNew();
    var grid = NaturalNeighborGrid3D.Discrete(samplePoints, sampleValues, nx, ny, nz, min, max);
    sw.Stop();
    var elapsedMs = sw.ElapsedMilliseconds;

    var rmse = ComputeRmse3D(grid, min, max);

    Console.WriteLine($"Sample points: {samplePoints.Count}, grid: {nx}x{ny}x{nz}");
    Console.WriteLine($"Discrete NaturalNeighborGrid3D: {elapsedMs,6} ms, RMSE = {rmse:0.0000}");
}

Run3DGridBenchmark();

Console.WriteLine("\nDone. Press any key to exit.");
Console.ReadKey();
