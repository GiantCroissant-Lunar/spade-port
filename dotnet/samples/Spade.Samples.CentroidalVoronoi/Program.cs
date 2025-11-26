using Spade.Primitives;
using Spade.Advanced.Voronoi;

Console.WriteLine("Spade.NET advanced sample - Centroidal Voronoi relaxation\n");

// Generate a deterministic set of random points in the unit square
var rng = new Random(42);
var initialPoints = new List<Point2<double>>();
var pointCount = 16;

for (int i = 0; i < pointCount; i++)
{
    var x = rng.NextDouble();
    var y = rng.NextDouble();
    initialPoints.Add(new Point2<double>(x, y));
}

PrintPoints("Initial generator points:", initialPoints);

// Use a simple square clipping domain [0,1] x [0,1]
var domain = new ClipPolygon(new[]
{
    new Point2<double>(0.0, 0.0),
    new Point2<double>(1.0, 0.0),
    new Point2<double>(1.0, 1.0),
    new Point2<double>(0.0, 1.0),
});

int iterations = 5;
double step = 1.0; // Full move to centroid each iteration

Console.WriteLine($"Running centroidal Voronoi relaxation: iterations={iterations}, step={step:F1}\n");

var relaxedPoints = CentroidalVoronoiRelaxation.RelaxPoints(initialPoints, domain, iterations, step);

PrintPoints("Relaxed generator points:", relaxedPoints);

// Compute displacement statistics
if (initialPoints.Count == relaxedPoints.Count)
{
    double totalDisplacement = 0.0;
    double minDisplacement = double.MaxValue;
    double maxDisplacement = 0.0;

    for (int i = 0; i < initialPoints.Count; i++)
    {
        var before = initialPoints[i];
        var after = relaxedPoints[i];

        var dx = after.X - before.X;
        var dy = after.Y - before.Y;
        var dist = Math.Sqrt(dx * dx + dy * dy);

        totalDisplacement += dist;
        if (dist < minDisplacement) minDisplacement = dist;
        if (dist > maxDisplacement) maxDisplacement = dist;
    }

    var avgDisplacement = totalDisplacement / initialPoints.Count;

    Console.WriteLine("Displacement statistics:");
    Console.WriteLine($"  Count: {initialPoints.Count}");
    Console.WriteLine($"  Min displacement: {minDisplacement:F6}");
    Console.WriteLine($"  Max displacement: {maxDisplacement:F6}");
    Console.WriteLine($"  Avg displacement: {avgDisplacement:F6}");
}
else
{
    Console.WriteLine("Point count changed during relaxation; displacement stats skipped.");
}

Console.WriteLine("\nDone. Press any key to exit.");
Console.ReadKey();

static void PrintPoints(string title, IReadOnlyList<Point2<double>> points)
{
    Console.WriteLine(title);
    for (int i = 0; i < points.Count; i++)
    {
        Console.WriteLine($"  [{i}] {points[i]}");
    }
    Console.WriteLine();
}
