using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Spade;
using Spade.Primitives;
using Spade.Handles;

namespace Spade.Tests.Validation;

internal static class TriangulationOracleComparison
{
    /// <summary>
    /// Compares a Spade triangulation against an oracle triangulation description.
    /// This is a .NET-only scaffold: oracle data is provided as an <see cref="OracleTriangulationOutput"/>.
    /// </summary>
    public static void AssertEquivalentToOracle(
        OracleTriangulationOutput oracle,
        DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>> triangulation)
    {
        triangulation.NumVertices.Should().Be(oracle.Points.Count);

        // Map oracle points to indices using exact coordinates.
        var indexByPoint = new Dictionary<(double X, double Y), int>(oracle.Points.Count);
        for (int i = 0; i < oracle.Points.Count; i++)
        {
            var p = oracle.Points[i];
            indexByPoint[(p.X, p.Y)] = i;
        }

        // Extract triangles from Spade's inner faces as index triples into oracle.Points.
        var spadeTriangles = new List<int[]>();
        foreach (var face in triangulation.InnerFaces())
        {
            var vertices = GetFaceVertexPositions(face);
            if (vertices.Count != 3)
            {
                throw new InvalidOperationException($"Expected triangular face, but found {vertices.Count} vertices.");
            }

            var indices = new int[3];
            for (int j = 0; j < 3; j++)
            {
                var pos = vertices[j];
                if (!indexByPoint.TryGetValue((pos.X, pos.Y), out var idx))
                {
                    throw new InvalidOperationException($"Spade vertex at ({pos.X}, {pos.Y}) not found in oracle points.");
                }
                indices[j] = idx;
            }

            Array.Sort(indices);
            spadeTriangles.Add(indices);
        }

        // Normalize oracle triangles: sort indices within each triangle and then sort the list.
        static IEnumerable<int[]> Normalize(IEnumerable<int[]> tris) =>
            tris.Select(t =>
            {
                var copy = (int[])t.Clone();
                Array.Sort(copy);
                return copy;
            })
            .OrderBy(t => t[0]).ThenBy(t => t[1]).ThenBy(t => t[2]);

        var oracleNormalized = Normalize(oracle.Triangles).ToList();
        var spadeNormalized = Normalize(spadeTriangles).ToList();

        spadeNormalized.Count.Should().Be(oracleNormalized.Count);
        for (int i = 0; i < oracleNormalized.Count; i++)
        {
            spadeNormalized[i].Should().Equal(oracleNormalized[i]);
        }
    }

    private static List<Point2<double>> GetFaceVertexPositions(FaceHandle<Point2<double>, int, int, int> face)
    {
        var result = new List<Point2<double>>();

        var startEdge = face.AdjacentEdge();
        if (startEdge is null)
        {
            return result;
        }

        var edge = startEdge.Value;
        var visited = new HashSet<int>();
        const int maxIterations = 1000;
        var iterations = 0;

        do
        {
            if (iterations++ >= maxIterations)
            {
                throw new InvalidOperationException($"Exceeded maximum iterations while walking face {face.Handle.Index}.");
            }

            if (!visited.Add(edge.Handle.Index))
            {
                throw new InvalidOperationException($"Detected cycle while walking face {face.Handle.Index} at edge {edge.Handle.Index}.");
            }

            var vertex = edge.From();
            var pos = vertex.Data.Position;
            result.Add(pos);

            edge = edge.Next();
        }
        while (edge.Handle.Index != startEdge.Value.Handle.Index);

        return result;
    }
}
