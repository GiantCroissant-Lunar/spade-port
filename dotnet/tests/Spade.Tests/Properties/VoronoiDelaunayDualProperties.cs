using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Spade;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests.Properties;

[Trait("Category", "PropertyTests")]
public class VoronoiDelaunayDualProperties
{
    [Theory]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(50)]
    public void VoronoiNeighborGraph_EqualsDelaunayNeighborGraph(int numPoints)
    {
        var points = GenerateRandomPoints(numPoints, seed: 4242);
        var triangulation = BuildTriangulation(points);

        var indexByPoint = new Dictionary<(double X, double Y), int>(points.Count);
        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            indexByPoint[(p.X, p.Y)] = i;
        }

        var delaunayNeighbors = BuildDelaunayNeighborGraph(triangulation, indexByPoint);
        var voronoiNeighbors = BuildVoronoiNeighborGraph(triangulation, indexByPoint);

        foreach (var kvp in delaunayNeighbors)
        {
            var i = kvp.Key;
            var delaunaySet = kvp.Value;

            voronoiNeighbors.Should().ContainKey(i);
            var voronoiSet = voronoiNeighbors[i];

            voronoiSet.OrderBy(x => x).Should().Equal(
                delaunaySet.OrderBy(x => x),
                $"Voronoi neighbors should match Delaunay neighbors for site {i}");
        }
    }

    private static List<Point2<double>> GenerateRandomPoints(int count, int seed)
    {
        var rng = new Random(seed + 2000);
        var points = new List<Point2<double>>();
        var seen = new HashSet<(double, double)>();

        while (points.Count < count)
        {
            var x = (rng.NextDouble() - 0.5) * 200;
            var y = (rng.NextDouble() - 0.5) * 200;

            x = Math.Round(x, 6);
            y = Math.Round(y, 6);

            if (seen.Add((x, y)))
            {
                points.Add(new Point2<double>(x, y));
            }
        }

        return points;
    }

    private static DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>
        BuildTriangulation(IEnumerable<Point2<double>> points)
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        foreach (var p in points)
        {
            triangulation.Insert(p);
        }

        return triangulation;
    }

    private static Dictionary<int, HashSet<int>> BuildDelaunayNeighborGraph(
        DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>> triangulation,
        IReadOnlyDictionary<(double X, double Y), int> indexByPoint)
    {
        var neighbors = new Dictionary<int, HashSet<int>>();

        foreach (var edge in triangulation.UndirectedEdges())
        {
            var handle = edge.Handle;
            var e0 = triangulation.DirectedEdge(new Spade.Handles.FixedDirectedEdgeHandle(handle.Index * 2));
            var fromPos = ((IHasPosition<double>)e0.From().Data).Position;
            var toPos = ((IHasPosition<double>)e0.To().Data).Position;

            if (!indexByPoint.TryGetValue((fromPos.X, fromPos.Y), out var i))
            {
                continue;
            }
            if (!indexByPoint.TryGetValue((toPos.X, toPos.Y), out var j))
            {
                continue;
            }

            if (!neighbors.TryGetValue(i, out var setI))
            {
                setI = new HashSet<int>();
                neighbors[i] = setI;
            }
            if (!neighbors.TryGetValue(j, out var setJ))
            {
                setJ = new HashSet<int>();
                neighbors[j] = setJ;
            }

            if (i != j)
            {
                setI.Add(j);
                setJ.Add(i);
            }
        }

        return neighbors;
    }

    private static Dictionary<int, HashSet<int>> BuildVoronoiNeighborGraph(
        DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>> triangulation,
        IReadOnlyDictionary<(double X, double Y), int> indexByPoint)
    {
        var neighbors = new Dictionary<int, HashSet<int>>();

        foreach (var face in triangulation.VoronoiFaces())
        {
            var vertex = face.AsDelaunayVertex();
            var pos = ((IHasPosition<double>)vertex.Data).Position;

            if (!indexByPoint.TryGetValue((pos.X, pos.Y), out var genIndex))
            {
                continue;
            }

            if (!neighbors.TryGetValue(genIndex, out var set))
            {
                set = new HashSet<int>();
                neighbors[genIndex] = set;
            }

            foreach (var edge in face.AdjacentEdges())
            {
                var dEdge = edge.AsDelaunayEdge();
                var from = ((IHasPosition<double>)dEdge.From().Data).Position;
                var to = ((IHasPosition<double>)dEdge.To().Data).Position;

                int neighborIndex;
                if (Math.Abs(from.X - pos.X) < 1e-9 && Math.Abs(from.Y - pos.Y) < 1e-9)
                {
                    if (!indexByPoint.TryGetValue((to.X, to.Y), out neighborIndex))
                    {
                        continue;
                    }
                }
                else if (Math.Abs(to.X - pos.X) < 1e-9 && Math.Abs(to.Y - pos.Y) < 1e-9)
                {
                    if (!indexByPoint.TryGetValue((from.X, from.Y), out neighborIndex))
                    {
                        continue;
                    }
                }
                else
                {
                    continue;
                }

                if (neighborIndex != genIndex)
                {
                    set.Add(neighborIndex);
                }
            }
        }

        return neighbors;
    }
}
