using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Spade;
using Spade.Primitives;
using Spade.Voronoi;

namespace Spade.Tests.Validation;

internal static class VoronoiOracleComparison
{
    /// <summary>
    /// Compares the Voronoi neighbor graph induced by a Spade triangulation
    /// against an oracle Voronoi description (generator index + neighbor indices).
    /// Coordinates must match between triangulation vertices and oracle points.
    /// </summary>
    public static void AssertEquivalentNeighborGraph(
        OracleVoronoiOutput oracle,
        IReadOnlyList<OraclePoint> oraclePoints,
        DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>> triangulation)
    {
        // Map point coordinates to oracle indices.
        var indexByPoint = new Dictionary<(double X, double Y), int>(oraclePoints.Count);
        for (int i = 0; i < oraclePoints.Count; i++)
        {
            var p = oraclePoints[i];
            indexByPoint[(p.X, p.Y)] = i;
        }

        // Build oracle neighbor sets.
        var oracleNeighbors = new Dictionary<int, HashSet<int>>();
        foreach (var cell in oracle.Cells)
        {
            if (!oracleNeighbors.TryGetValue(cell.GeneratorIndex, out var set))
            {
                set = new HashSet<int>();
                oracleNeighbors[cell.GeneratorIndex] = set;
            }

            foreach (var n in cell.Neighbors)
            {
                set.Add(n);
            }
        }

        // Build Spade neighbor sets using VoronoiFaces / AdjacentEdges.
        var spadeNeighbors = new Dictionary<int, HashSet<int>>();

        foreach (var face in triangulation.VoronoiFaces())
        {
            var vertex = face.AsDelaunayVertex();
            var pos = vertex.Data.Position;

            if (!indexByPoint.TryGetValue((pos.X, pos.Y), out var genIndex))
            {
                // Ignore vertices not in oracle (for this scaffold we expect them to match).
                continue;
            }

            if (!spadeNeighbors.TryGetValue(genIndex, out var neighbors))
            {
                neighbors = new HashSet<int>();
                spadeNeighbors[genIndex] = neighbors;
            }

            foreach (var edge in face.AdjacentEdges())
            {
                var delaunayEdge = edge.AsDelaunayEdge();
                var from = delaunayEdge.From();
                var to = delaunayEdge.To();

                var fromPos = from.Data.Position;
                var toPos = to.Data.Position;

                int neighborIndex;
                if (fromPos.X == pos.X && fromPos.Y == pos.Y)
                {
                    if (!indexByPoint.TryGetValue((toPos.X, toPos.Y), out neighborIndex))
                    {
                        throw new InvalidOperationException($"Neighbor vertex at ({toPos.X}, {toPos.Y}) not found in oracle points.");
                    }
                }
                else if (toPos.X == pos.X && toPos.Y == pos.Y)
                {
                    if (!indexByPoint.TryGetValue((fromPos.X, fromPos.Y), out neighborIndex))
                    {
                        throw new InvalidOperationException($"Neighbor vertex at ({fromPos.X}, {fromPos.Y}) not found in oracle points.");
                    }
                }
                else
                {
                    // Edge not incident to this generator; skip.
                    continue;
                }

                if (neighborIndex != genIndex)
                {
                    neighbors.Add(neighborIndex);
                }
            }
        }

        // Compare graphs for generators that exist in oracle.
        foreach (var kvp in oracleNeighbors)
        {
            var genIndex = kvp.Key;
            var oracleSet = kvp.Value;

            spadeNeighbors.Should().ContainKey(genIndex);
            var spadeSet = spadeNeighbors[genIndex];

            spadeSet.OrderBy(x => x).Should().Equal(oracleSet.OrderBy(x => x));
        }
    }
}
