using System;
using System.Collections.Generic;
using Spade;
using Spade.Handles;
using Spade.Primitives;

namespace Spade.Advanced.Power;

public sealed class WeightedDelaunayTriangulation
{
    private readonly DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>> _triangulation;
    private readonly List<WeightedPoint> _sites;

    public WeightedDelaunayTriangulation()
    {
        _triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        _sites = new List<WeightedPoint>();
    }

    public DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>> Triangulation => _triangulation;

    public IReadOnlyList<WeightedPoint> Sites => _sites;

    public int VertexCount => _triangulation.NumVertices;

    public int Insert(WeightedPoint site)
    {
        var handle = _triangulation.Insert(site.Position);
        var index = handle.Index;
        EnsureSitesCapacity(index + 1);
        _sites[index] = site;
        return index;
    }

    public void InsertRange(IEnumerable<WeightedPoint> sites)
    {
        if (sites is null) throw new ArgumentNullException(nameof(sites));
        foreach (var site in sites)
        {
            Insert(site);
        }
    }

    private void EnsureSitesCapacity(int size)
    {
        while (_sites.Count < size)
        {
            _sites.Add(default);
        }
    }

    /// <summary>
    /// Builds a symmetric neighbor graph between vertices based on the Delaunay triangulation.
    /// </summary>
    public List<HashSet<int>> BuildNeighborGraph()
    {
        var vertexCount = _triangulation.NumVertices;
        var neighbors = new List<HashSet<int>>(vertexCount);
        for (var i = 0; i < vertexCount; i++)
        {
            neighbors.Add(new HashSet<int>());
        }

        for (var i = 0; i < vertexCount; i++)
        {
            var vertex = _triangulation.Vertex(new FixedVertexHandle(i));
            var startEdge = vertex.OutEdge();
            if (!startEdge.HasValue)
            {
                continue;
            }

            var current = startEdge.Value;
            var visitedEdges = new HashSet<int>();
            var maxIterations = 1000;
            var iterations = 0;

            do
            {
                if (iterations++ >= maxIterations)
                {
                    throw new InvalidOperationException(
                        $"Exceeded maximum iterations ({maxIterations}) while collecting neighbors for vertex {i}.");
                }

                if (!visitedEdges.Add(current.Handle.Index))
                {
                    throw new InvalidOperationException(
                        $"Detected cycle while collecting neighbors for vertex {i} at edge {current.Handle.Index}.");
                }

                var to = current.To();
                var j = to.Handle.Index;

                if (j >= 0 && j < vertexCount && j != i)
                {
                    neighbors[i].Add(j);
                    neighbors[j].Add(i);
                }

                current = current.CCW();
            } while (current.Handle.Index != startEdge.Value.Handle.Index);
        }

        return neighbors;
    }
}
