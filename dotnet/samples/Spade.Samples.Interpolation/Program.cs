using Spade;
using Spade.Primitives;

Console.WriteLine("Spade.NET sample - Barycentric and Natural Neighbor interpolation\n");

// Target function to approximate
static double TrueFunction(Point2<double> p) => Math.Sin(p.X) + Math.Cos(p.Y);

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
