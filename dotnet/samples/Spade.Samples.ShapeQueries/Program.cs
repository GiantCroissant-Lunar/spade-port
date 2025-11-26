using Spade;
using Spade.Primitives;
using Spade.Handles;

Console.WriteLine("Spade.NET sample - Shape / region queries\n");

// Build a triangulation over random points in [-1,1] x [-1,1]
var tri = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

var rng = new Random(123);
int pointCount = 100;
for (int i = 0; i < pointCount; i++)
{
    var x = rng.NextDouble() * 2.0 - 1.0; // [-1,1]
    var y = rng.NextDouble() * 2.0 - 1.0;
    tri.Insert(new Point2<double>(x, y));
}

Console.WriteLine($"Inserted {tri.NumVertices} points into triangulation.\n");

var rectLower = new Point2<double>(-0.5, -0.5);
var rectUpper = new Point2<double>(0.5, 0.5);
var circleCenter = new Point2<double>(0.0, 0.0);
var circleRadius = 0.6;
var circleRadiusSquared = circleRadius * circleRadius;

// Edges and vertices in rectangle
var rectEdges = tri.GetEdgesInRectangle(rectLower, rectUpper).ToList();
var rectVertices = tri.GetVerticesInRectangle(rectLower, rectUpper).ToList();

// Edges and vertices in circle
var circleEdges = tri.GetEdgesInCircle(circleCenter, circleRadiusSquared).ToList();
var circleVertices = tri.GetVerticesInCircle(circleCenter, circleRadiusSquared).ToList();

Console.WriteLine($"Rectangle [{rectLower} .. {rectUpper}]:");
Console.WriteLine($"  Vertices in rectangle: {rectVertices.Count}");
Console.WriteLine($"  Edges in rectangle:    {rectEdges.Count}");

Console.WriteLine();
Console.WriteLine($"Circle center={circleCenter}, r={circleRadius:F2}:");
Console.WriteLine($"  Vertices in circle: {circleVertices.Count}");
Console.WriteLine($"  Edges in circle:    {circleEdges.Count}");

// Print a few sample edges from the circle query
Console.WriteLine();
Console.WriteLine("Sample edges in circle (up to 5):");
for (int i = 0; i < Math.Min(5, circleEdges.Count); i++)
{
    var undirected = circleEdges[i];
    var fixedEdge = undirected.Handle;
    var directed = tri.DirectedEdge(new FixedDirectedEdgeHandle(fixedEdge.Index * 2));
    var from = ((IHasPosition<double>)directed.From().Data).Position;
    var to = ((IHasPosition<double>)directed.To().Data).Position;
    Console.WriteLine($"  Edge {fixedEdge.Index}: {from} -> {to}");
}

Console.WriteLine("\nDone. Press any key to exit.");
Console.ReadKey();
