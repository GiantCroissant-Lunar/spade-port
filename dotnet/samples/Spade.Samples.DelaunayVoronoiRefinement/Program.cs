using Spade;
using Spade.Primitives;
using Spade.Refinement;
using Spade.Handles;

Console.WriteLine("Spade.NET sample - Delaunay, Voronoi, and refinement\n");

// Build a simple Delaunay triangulation
var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

var points = new[]
{
    new Point2<double>(0.0, 0.0),
    new Point2<double>(1.0, 0.0),
    new Point2<double>(0.0, 1.0),
    new Point2<double>(1.0, 1.0),
    new Point2<double>(0.5, 0.3),
    new Point2<double>(0.2, 0.7),
    new Point2<double>(0.8, 0.6),
};

foreach (var p in points)
{
    triangulation.Insert(p);
}

Console.WriteLine($"Inserted {points.Length} points into Delaunay triangulation.");
Console.WriteLine("Inner faces (triangles):");

var triangleIndex = 0;
double delaunayTotalArea = 0.0;
double delaunayMinArea = double.MaxValue;
double delaunayMaxArea = 0.0;
int delaunayFaceCount = 0;

foreach (var face in triangulation.InnerFaces())
{
    var edge = face.AdjacentEdge();
    if (edge == null) continue;

    var e0 = edge.Value;
    var p0 = e0.From().Data.Position;
    var p1 = e0.To().Data.Position;
    var p2 = e0.Next().To().Data.Position;

    var ax = p1.X - p0.X;
    var ay = p1.Y - p0.Y;
    var bx = p2.X - p0.X;
    var by = p2.Y - p0.Y;
    var cross = ax * by - ay * bx;
    var area = Math.Abs(cross) * 0.5;

    delaunayTotalArea += area;
    if (area < delaunayMinArea) delaunayMinArea = area;
    if (area > delaunayMaxArea) delaunayMaxArea = area;
    delaunayFaceCount++;

    Console.WriteLine($"[{triangleIndex++}] {p0}, {p1}, {p2} | area={area:F6}");
}

if (delaunayFaceCount > 0)
{
    var delaunayAvgArea = delaunayTotalArea / delaunayFaceCount;
    Console.WriteLine();
    Console.WriteLine($"Delaunay triangle area stats: count={delaunayFaceCount}, min={delaunayMinArea:F6}, max={delaunayMaxArea:F6}, avg={delaunayAvgArea:F6}");
}
else
{
    Console.WriteLine("No inner faces in triangulation yet.");
}

Console.WriteLine();
Console.WriteLine("Voronoi sites:");

foreach (var voronoiFace in triangulation.VoronoiFaces())
{
    var site = voronoiFace.AsDelaunayVertex();
    var pos = site.Data.Position;
    Console.WriteLine($"Site at {pos}");
}

// Constrained Delaunay triangulation with refinement
Console.WriteLine();
Console.WriteLine("Constrained Delaunay triangulation and mesh refinement:");

var cdt = new ConstrainedDelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();

// Square boundary
var v0 = cdt.Insert(new Point2<double>(0.0, 0.0));
var v1 = cdt.Insert(new Point2<double>(1.0, 0.0));
var v2 = cdt.Insert(new Point2<double>(1.0, 1.0));
var v3 = cdt.Insert(new Point2<double>(0.0, 1.0));

cdt.AddConstraint(v0, v1);
cdt.AddConstraint(v1, v2);
cdt.AddConstraint(v2, v3);
cdt.AddConstraint(v3, v0);

// Interior points
var interiorPoints = new[]
{
    new Point2<double>(0.5, 0.2),
    new Point2<double>(0.7, 0.5),
    new Point2<double>(0.4, 0.8),
};

foreach (var p in interiorPoints)
{
    cdt.Insert(p);
}

// Triangle statistics before refinement
double beforeTotalArea = 0.0;
double beforeMinArea = double.MaxValue;
double beforeMaxArea = 0.0;
int beforeFaceCount = 0;

foreach (var face in cdt.InnerFaces())
{
	var edge = face.AdjacentEdge();
	if (edge == null) continue;

	var e0 = edge.Value;
	var p0 = e0.From().Data.Position;
	var p1 = e0.To().Data.Position;
	var p2 = e0.Next().To().Data.Position;

	var ax = p1.X - p0.X;
	var ay = p1.Y - p0.Y;
	var bx = p2.X - p0.X;
	var by = p2.Y - p0.Y;
	var cross = ax * by - ay * bx;
	var area = Math.Abs(cross) * 0.5;

	beforeTotalArea += area;
	if (area < beforeMinArea) beforeMinArea = area;
	if (area > beforeMaxArea) beforeMaxArea = area;
	beforeFaceCount++;
}

if (beforeFaceCount > 0)
{
	var beforeAvgArea = beforeTotalArea / beforeFaceCount;
	Console.WriteLine($"Before refinement: triangles={beforeFaceCount}, minArea={beforeMinArea:F6}, maxArea={beforeMaxArea:F6}, avgArea={beforeAvgArea:F6}");
}

Console.WriteLine("Refining constrained triangulation...");

var parameters = new RefinementParameters()
    .WithMaxAllowedArea(0.01)
    .WithAngleLimit(AngleLimit.FromDegrees(25));

var refinementResult = cdt.Refine(parameters);
Console.WriteLine($"Refinement added {refinementResult.AddedVertices} Steiner points.");

// Triangle statistics after refinement
double afterTotalArea = 0.0;
double afterMinArea = double.MaxValue;
double afterMaxArea = 0.0;
int afterFaceCount = 0;

foreach (var face in cdt.InnerFaces())
{
	var edge = face.AdjacentEdge();
	if (edge == null) continue;

	var e0 = edge.Value;
	var p0 = e0.From().Data.Position;
	var p1 = e0.To().Data.Position;
	var p2 = e0.Next().To().Data.Position;

	var ax = p1.X - p0.X;
	var ay = p1.Y - p0.Y;
	var bx = p2.X - p0.X;
	var by = p2.Y - p0.Y;
	var cross = ax * by - ay * bx;
	var area = Math.Abs(cross) * 0.5;

	afterTotalArea += area;
	if (area < afterMinArea) afterMinArea = area;
	if (area > afterMaxArea) afterMaxArea = area;
	afterFaceCount++;
}

if (afterFaceCount > 0)
{
	var afterAvgArea = afterTotalArea / afterFaceCount;
	Console.WriteLine($"After refinement: triangles={afterFaceCount}, minArea={afterMinArea:F6}, maxArea={afterMaxArea:F6}, avgArea={afterAvgArea:F6}");
}

Console.WriteLine("\nDone. Press any key to exit.");
Console.ReadKey();
