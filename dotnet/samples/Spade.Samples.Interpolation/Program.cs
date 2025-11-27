using System.Diagnostics;
using Spade;
using Spade.Primitives;
using Spade.Advanced.Interpolation;

Console.WriteLine("Spade.NET sample - Barycentric and Natural Neighbor interpolation\n");

// Target function to approximate
static double TrueFunction(Point2<double> p) => Math.Sin(p.X) + Math.Cos(p.Y);

static double ComputeRmse(double[,] grid, Point2<double> min, Point2<double> max)
{
    var height = grid.GetLength(0);
    var width = grid.GetLength(1);

    var dx = width == 1 ? 0.0 : (max.X - min.X) / (width - 1);
    var dy = height == 1 ? 0.0 : (max.Y - min.Y) / (height - 1);

    var sumSq = 0.0;
    var count = 0;

    for (int iy = 0; iy < height; iy++)
    {
        var y = height == 1 ? 0.5 * (min.Y + max.Y) : min.Y + iy * dy;
        for (int ix = 0; ix < width; ix++)
        {
            var x = width == 1 ? 0.5 * (min.X + max.X) : min.X + ix * dx;
            var v = grid[iy, ix];
            if (double.IsNaN(v) || double.IsInfinity(v))
            {
                continue;
            }

            var truth = TrueFunction(new Point2<double>(x, y));
            var diff = v - truth;
            sumSq += diff * diff;
            count++;
        }
    }

    return count > 0 ? Math.Sqrt(sumSq / count) : double.NaN;
}

static void RunGridBenchmark()
{
    Console.WriteLine();
    Console.WriteLine("Grid interpolation benchmark (exact vs discrete)\n");

    const int samplePerAxis = 41;
    const double minCoord = -2.0;
    const double maxCoord = 2.0;

    var samplePoints = new List<Point2<double>>();
    var sampleValues = new List<double>();

    for (int iy = 0; iy < samplePerAxis; iy++)
    {
        var ty = samplePerAxis == 1 ? 0.5 : iy / (double)(samplePerAxis - 1);
        var y = minCoord + ty * (maxCoord - minCoord);
        for (int ix = 0; ix < samplePerAxis; ix++)
        {
            var tx = samplePerAxis == 1 ? 0.5 : ix / (double)(samplePerAxis - 1);
            var x = minCoord + tx * (maxCoord - minCoord);

            var p = new Point2<double>(x, y);
            samplePoints.Add(p);
            sampleValues.Add(TrueFunction(p));
        }
    }

    var min = new Point2<double>(minCoord, minCoord);
    var max = new Point2<double>(maxCoord, maxCoord);

    const int width = 256;
    const int height = 256;

    var sw = Stopwatch.StartNew();
    var exactGrid = NaturalNeighborGrid2D.Exact(samplePoints, sampleValues, width, height, min, max);
    sw.Stop();
    var exactMs = sw.ElapsedMilliseconds;

    sw.Restart();
    var discreteGrid = NaturalNeighborGrid2D.Discrete(samplePoints, sampleValues, width, height, min, max);
    sw.Stop();
    var discreteMs = sw.ElapsedMilliseconds;

    var exactRmse = ComputeRmse(exactGrid, min, max);
    var discreteRmse = ComputeRmse(discreteGrid, min, max);

    Console.WriteLine($"Sample points: {samplePoints.Count}, grid: {width}x{height}");
    Console.WriteLine($"Exact GridNaturalNeighbor2D:    {exactMs,6} ms, RMSE = {exactRmse:0.0000}");
    Console.WriteLine($"DiscreteGridNaturalNeighbor2D: {discreteMs,6} ms, RMSE = {discreteRmse:0.0000}");
}

// Build a Delaunay triangulation over a small grid of sample points
var tri = new DelaunayTriangulation<SamplePoint, int, int, int, LastUsedVertexHintGenerator<double>>();

var coords = new[] { -1.0, -0.5, 0.0, 0.5, 1.0 };
foreach (var x in coords)
{
    foreach (var y in coords)
    {
        var p = new Point2<double>(x, y);
        var value = TrueFunction(p);
        tri.Insert(new SamplePoint(p, value));
    }
}

Console.WriteLine($"Inserted {tri.NumVertices} sample points into triangulation.\n");

var bary = tri.Barycentric();
var nn = tri.NaturalNeighbor();

// Query points inside the domain
var queries = new List<Point2<double>>
{
    new(-0.8, -0.2),
    new(-0.1, 0.6),
    new(0.0, 0.0),
    new(0.7, -0.3),
    new(0.4, 0.9),
};

Console.WriteLine("Query point interpolation (true vs. barycentric vs. natural neighbor):");
Console.WriteLine("  x, y | true | bary (err) | nn (err)");

foreach (var q in queries)
{
    var truth = TrueFunction(q);

    var baryVal = bary.Interpolate(v => ((SamplePoint)v.Data).Value, q);
    var nnVal = nn.Interpolate(v => ((SamplePoint)v.Data).Value, q);

    string baryText = baryVal.HasValue
        ? $"{baryVal.Value,7:F4} (err={Math.Abs(baryVal.Value - truth),7:F4})"
        : "   null";

    string nnText = nnVal.HasValue
        ? $"{nnVal.Value,7:F4} (err={Math.Abs(nnVal.Value - truth),7:F4})"
        : "   null";

    Console.WriteLine(
        $"  {q.X,4:F1}, {q.Y,4:F1} | {truth,7:F4} | {baryText} | {nnText}");
}

Console.WriteLine();
Console.WriteLine("Natural neighbor gradient-based interpolation:");
Console.WriteLine("  x, y | true | nn_grad (err)");

var grads = nn.EstimateGradients(v => ((SamplePoint)v.Data).Value);

foreach (var q in queries)
{
	var truth = TrueFunction(q);

	var nnGradVal = nn.InterpolateGradient(
		v => ((SamplePoint)v.Data).Value,
		grads,
		flatness: 1.0,
		position: q);

	string nnGradText = nnGradVal.HasValue
		? $"{nnGradVal.Value,7:F4} (err={Math.Abs(nnGradVal.Value - truth),7:F4})"
		: "   null";

	Console.WriteLine($"  {q.X,4:F1}, {q.Y,4:F1} | {truth,7:F4} | {nnGradText}");
}

RunGridBenchmark();

Console.WriteLine("\nDone. Press any key to exit.");
Console.ReadKey();

// Simple data type that carries both position and a scalar value.
struct SamplePoint : Spade.Primitives.IHasPosition<double>
{
    public Point2<double> Position { get; }
    public double Value { get; }

    public SamplePoint(Point2<double> position, double value)
    {
        Position = position;
        Value = value;
    }
}
