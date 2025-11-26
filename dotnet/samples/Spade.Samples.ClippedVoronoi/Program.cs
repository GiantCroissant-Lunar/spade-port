using Spade;
using Spade.Primitives;
using Spade.Advanced.Voronoi;

Console.WriteLine("Spade.NET advanced sample - Clipped Voronoi in polygonal domain\n");

// Build a Delaunay triangulation over a set of generator points.
var tri = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

var generators = new List<Point2<double>>
{
    new(0.1, 0.1),
    new(0.9, 0.1),
    new(0.1, 0.9),
    new(0.9, 0.9),
    new(0.3, 0.3),
    new(0.7, 0.3),
    new(0.3, 0.7),
    new(0.7, 0.7),
    new(0.5, 0.5),
};

foreach (var p in generators)
{
    tri.Insert(p);
}

Console.WriteLine($"Inserted {tri.NumVertices} generator points.\n");

// Define a convex polygonal domain (a smaller rotated-like rectangle inside [0,1]x[0,1]).
var domain = new ClipPolygon(new[]
{
    new Point2<double>(0.2, 0.2),
    new Point2<double>(0.8, 0.2),
    new Point2<double>(0.9, 0.5),
    new Point2<double>(0.8, 0.8),
    new Point2<double>(0.2, 0.8),
    new Point2<double>(0.1, 0.5),
});

Console.WriteLine("Domain polygon vertices (CCW):");
for (int i = 0; i < domain.Vertices.Count; i++)
{
    Console.WriteLine($"  [{i}] {domain.Vertices[i]}");
}
Console.WriteLine();

// Build the clipped Voronoi diagram
var clipped = ClippedVoronoiBuilder.ClipToPolygon(tri, domain);

Console.WriteLine("Clipped Voronoi cells:");
foreach (var cell in clipped.Cells)
{
    var site = cell.Generator;
    var poly = cell.Polygon;
    var area = ComputePolygonArea(poly);

    Console.WriteLine($"Site {site}:");
    Console.WriteLine($"  Polygon vertices: {poly.Count}, area≈{area:F4}, isClipped={cell.IsClipped}");
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
