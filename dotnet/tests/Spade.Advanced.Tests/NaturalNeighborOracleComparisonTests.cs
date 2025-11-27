using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Spade;
using Spade.Handles;
using Spade.Primitives;
using Xunit;

namespace Spade.Advanced.Tests;

public class NaturalNeighborOracleComparisonTests
{
    private readonly struct PointWithValue : IHasPosition<double>
    {
        public Point2<double> Position { get; }
        public double Value { get; }
        public int Index { get; }

        public PointWithValue(Point2<double> position, double value, int index)
        {
            Position = position;
            Value = value;
            Index = index;
        }
    }

    [Fact(Skip = "Oracle NNI JSON not yet provided; generate via Julia/Rust and remove Skip to enable.")]
    public void NaturalNeighbor_MatchesOracleValuesAndWeights_WhenOracleAvailable()
    {
        var repoRoot = FindRepoRoot();
        var oraclePath = Path.Combine(
            repoRoot,
            "oracle-tools",
            "nni-oracle",
            "simple_case.json");

        var oracle = OracleNniJson.ReadFromFile(oraclePath);

        var triangulation = new DelaunayTriangulation<PointWithValue, int, int, int, LastUsedVertexHintGenerator<double>>();
        for (int i = 0; i < oracle.Points.Count; i++)
        {
            var p = oracle.Points[i];
            var v = oracle.Values[i];
            triangulation.Insert(new PointWithValue(new Point2<double>(p.X, p.Y), v, i));
        }

        var nn = triangulation.NaturalNeighbor();

        foreach (var q in oracle.Queries)
        {
            var pos = new Point2<double>(q.X, q.Y);

            // Compare interpolated values.
            var value = nn.Interpolate(v => ((PointWithValue)v.Data).Value, pos);
            value.Should().NotBeNull();
            value!.Value.Should().BeApproximately(q.Value, 1e-6);

            // Compare weight distributions (sparse, by point index).
            var weights = new List<(FixedVertexHandle Vertex, double Weight)>();
            nn.GetWeights(pos, weights);

            var actualByIndex = new Dictionary<int, double>();
            foreach (var (vertex, w) in weights)
            {
                var data = (PointWithValue)triangulation.Vertex(vertex).Data;
                if (actualByIndex.TryGetValue(data.Index, out var existing))
                {
                    actualByIndex[data.Index] = existing + w;
                }
                else
                {
                    actualByIndex[data.Index] = w;
                }
            }

            var expectedByIndex = new Dictionary<int, double>();
            foreach (var entry in q.Weights)
            {
                expectedByIndex[entry.PointIndex] = entry.Weight;
            }

            foreach (var (idx, expectedWeight) in expectedByIndex)
            {
                actualByIndex.Should().ContainKey(idx);
                actualByIndex[idx].Should().BeApproximately(expectedWeight, 1e-6);
            }
        }
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "Spade.sln");
            if (File.Exists(candidate))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir)!;
        }

        throw new InvalidOperationException("Could not locate repo root (Spade.sln) starting from test base directory.");
    }
}
