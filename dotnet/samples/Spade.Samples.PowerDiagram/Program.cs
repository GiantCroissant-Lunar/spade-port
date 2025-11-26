using Spade.Primitives;
using Spade.Advanced.Power;

Console.WriteLine("Spade.NET advanced sample - Power diagram / weighted Voronoi\n");

// Define a small set of sites with different weights.
// Lower weight means a larger cell in power-distance terms.
var sites = new List<Point2<double>>
{
    new(0.2, 0.2),
    new(0.8, 0.2),
    new(0.2, 0.8),
    new(0.8, 0.8),
    new(0.5, 0.5),
};

var weights = new List<double>
{
    0.0,   // baseline
    0.0,   // baseline
    0.5,   // slightly penalized
    1.0,   // more penalized (smaller cell)
   -0.5,   // favored (larger cell)
};

Console.WriteLine("Sites (position, weight):");
for (int i = 0; i < sites.Count; i++)
{
    Console.WriteLine($"  [{i}] pos={sites[i]}, weight={weights[i]:F2}");
}
Console.WriteLine();

// Build the power diagram
var diagram = PowerDiagramBuilder.Build(sites, weights);

// Inspect cells: polygon vertex count, approximate area, neighbors
Console.WriteLine("Power cells:");
for (int i = 0; i < diagram.Cells.Count; i++)
{
    var cell = diagram.Cells[i];
    var poly = cell.Polygon;
    var neighborIndices = string.Join(", ", cell.NeighborSiteIndices);
    var area = ComputePolygonArea(poly);

    Console.WriteLine($"Cell {cell.SiteIndex}: site={cell.Site.Position}, w={cell.Site.Weight:F2}");
    Console.WriteLine($"  Neighbors: [{neighborIndices}]");
    Console.WriteLine($"  Polygon vertices: {poly.Count}, area≈{area:F4}");
}

Console.WriteLine();

// Demonstrate nearest-site queries in power-distance sense
var queryPoints = new List<Point2<double>>
{
    new(0.15, 0.15),
    new(0.85, 0.15),
    new(0.15, 0.85),
    new(0.85, 0.85),
    new(0.5, 0.5),
    new(0.5, 0.2),
};

Console.WriteLine("Power-distance nearest-site queries:");
foreach (var q in queryPoints)
{
    var idx = PowerDiagramQueries.FindNearestSiteIndex(diagram.Sites, q);
    var site = diagram.Sites[idx];
    Console.WriteLine($"  Query {q} → nearest site index={idx}, pos={site.Position}, weight={site.Weight:F2}");
}

Console.WriteLine("\nDone. Press any key to exit.");
Console.ReadKey();

static double ComputePolygonArea(IReadOnlyList<Point2<double>> polygon)
{
    if (polygon.Count < 3) return 0.0;

    double sum = 0.0;
    for (int i = 0; i < polygon.Count; i++)
    {
        var p0 = polygon[i];
        var p1 = polygon[(i + 1) % polygon.Count];
        sum += (p0.X * p1.Y - p0.Y * p1.X);
    }

    return Math.Abs(sum) * 0.5;
}
