using Spade.Primitives;
using Spade.Advanced.Voronoi;

Console.WriteLine("Spade.NET sample - Textbook Lloyd relaxation (centroidal Voronoi)\n");

// Parameters
int pointCount = 64;
int iterations = 20;
double step = 1.0; // Full Lloyd step

// Initial random points in unit square [0,1] x [0,1]
var rng = new Random(1234);
var points = new List<Point2<double>>(pointCount);
for (int i = 0; i < pointCount; i++)
{
    var x = rng.NextDouble();
    var y = rng.NextDouble();
    points.Add(new Point2<double>(x, y));
}

// Convex domain: unit square
var domain = new ClipPolygon(new[]
{
    new Point2<double>(0.0, 0.0),
    new Point2<double>(1.0, 0.0),
    new Point2<double>(1.0, 1.0),
    new Point2<double>(0.0, 1.0),
});

Console.WriteLine($"Initial generator count: {points.Count}");
Console.WriteLine($"Iterations: {iterations}, step={step:F1}\n");

Console.WriteLine("Iter | minNN  maxNN  meanNN  stdNN");
Console.WriteLine("-----+----------------------------");

for (int iter = 0; iter <= iterations; iter++)
{
    // Compute nearest-neighbor distance statistics
    var nnStats = ComputeNearestNeighborStats(points);
    Console.WriteLine(
        $"{iter,4} | {nnStats.min,5:F3} {nnStats.max,5:F3} {nnStats.mean,6:F3} {nnStats.stdDev,6:F3}");

    if (iter == iterations)
    {
        break;
    }

    // One Lloyd step: relax points toward centroids of clipped Voronoi cells
    points = CentroidalVoronoiRelaxation.RelaxPoints(points, domain, iterations: 1, step: step).ToList();
}

Console.WriteLine("\nFinal generator positions (first 10):");
for (int i = 0; i < Math.Min(10, points.Count); i++)
{
    Console.WriteLine($"  [{i}] {points[i]}");
}

Console.WriteLine("\nDone. Press any key to exit.");
Console.ReadKey();

static (double min, double max, double mean, double stdDev) ComputeNearestNeighborStats(IReadOnlyList<Point2<double>> pts)
{
    if (pts.Count <= 1)
    {
        return (0.0, 0.0, 0.0, 0.0);
    }

    var distances = new double[pts.Count];

    for (int i = 0; i < pts.Count; i++)
    {
        double best = double.MaxValue;
        var pi = pts[i];
        for (int j = 0; j < pts.Count; j++)
        {
            if (i == j) continue;
            var pj = pts[j];
            var dx = pi.X - pj.X;
            var dy = pi.Y - pj.Y;
            var d2 = dx * dx + dy * dy;
            if (d2 < best)
            {
                best = d2;
            }
        }
        distances[i] = Math.Sqrt(best);
    }

    double min = distances[0];
    double max = distances[0];
    double sum = 0.0;
    for (int i = 0; i < distances.Length; i++)
    {
        var d = distances[i];
        if (d < min) min = d;
        if (d > max) max = d;
        sum += d;
    }

    double mean = sum / distances.Length;
    double varSum = 0.0;
    for (int i = 0; i < distances.Length; i++)
    {
        var diff = distances[i] - mean;
        varSum += diff * diff;
    }

    double stdDev = Math.Sqrt(varSum / distances.Length);
    return (min, max, mean, stdDev);
}
