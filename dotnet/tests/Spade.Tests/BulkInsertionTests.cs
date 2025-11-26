using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests;

public class BulkInsertionTests
{
    [Fact]
    public void InsertBulk_MatchesManualInsertion()
    {
        var points = new List<Point2<double>>
        {
            new(0.0, 0.0),
            new(1.0, 0.0),
            new(0.0, 1.0),
            new(1.0, 1.0),
            new(0.5, 0.5)
        };

        var triManual = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        foreach (var p in points)
        {
            triManual.Insert(p);
        }

        var triBulk = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        triBulk.InsertBulk(points, useSpatialSort: false);

        triBulk.NumVertices.Should().Be(triManual.NumVertices);
        triBulk.NumFaces.Should().Be(triManual.NumFaces);

        var manualPositions = triManual.Vertices().Select(v => v.Data.Position).OrderBy(p => (p.X, p.Y)).ToList();
        var bulkPositions = triBulk.Vertices().Select(v => v.Data.Position).OrderBy(p => (p.X, p.Y)).ToList();

        bulkPositions.Should().Equal(manualPositions);
    }

    [Fact]
    public void InsertBulk_WithSpatialSort_InsertsAllVertices()
    {
        var points = Enumerable.Range(0, 50)
            .Select(i => new Point2<double>(i % 10, i / 10))
            .ToList();

        var tri = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        tri.InsertBulk(points, useSpatialSort: true);

        tri.NumVertices.Should().Be(points.Count);
    }
}
