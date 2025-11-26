using Spade;
using Spade.Primitives;

var points = new[]
{
    new Point2<double>(0.0, 0.0),
    new Point2<double>(1.0, 0.0),
    new Point2<double>(2.0, 0.0),
    new Point2<double>(0.0, 1.0),
    new Point2<double>(1.0, 1.0),
    new Point2<double>(2.0, 1.0),
    new Point2<double>(0.0, 2.0),
    new Point2<double>(1.0, 2.0),
    new Point2<double>(2.0, 2.0),
};

var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
foreach (var p in points)
{
    triangulation.Insert(p);
}

Console.WriteLine($"Vertices: {triangulation.NumVertices}");
Console.WriteLine($"Inner Faces: {triangulation.InnerFaces().Count()}");
Console.WriteLine("\nTriangles (as vertex indices):");

var indexByPoint = new Dictionary<(double X, double Y), int>(points.Length);
for (int i = 0; i < points.Length; i++)
{
    indexByPoint[(points[i].X, points[i].Y)] = i;
}

foreach (var face in triangulation.InnerFaces())
{
    var edge = face.AdjacentEdge();
    if (edge == null) continue;
    
    var vertices = new List<int>();
    var start = edge.Value;
    var current = start;
    do
    {
        var pos = ((IHasPosition<double>)current.From().Data).Position;
        var idx = indexByPoint[(pos.X, pos.Y)];
        vertices.Add(idx);
        current = current.Next();
    } while (current.Handle.Index != start.Handle.Index);
    
    vertices.Sort();
    Console.WriteLine($"[{string.Join(", ", vertices)}]");
}
