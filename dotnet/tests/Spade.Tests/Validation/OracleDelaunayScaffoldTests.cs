using System.Collections.Generic;
using FluentAssertions;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests.Validation;

public class OracleDelaunayScaffoldTests
{
    [Fact]
    public void OracleInput_CanBeUsedToBuildSimpleDelaunayTriangulation()
    {
        var input = new OracleInput(
            Points: new List<OraclePoint>
            {
                new(0.0, 0.0),
                new(1.0, 0.0),
                new(0.0, 1.0),
            },
            Weights: null,
            Domain: null);

        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        foreach (var p in input.Points)
        {
            triangulation.Insert(new Point2<double>(p.X, p.Y));
        }

        triangulation.NumVertices.Should().Be(3);
        triangulation.NumFaces.Should().Be(2); // Outer + 1 inner
        triangulation.NumUndirectedEdges.Should().Be(3);
    }

    [Fact]
    public void OracleInput_Quad_ProducesExpectedTopology()
    {
        var input = new OracleInput(
            Points: new List<OraclePoint>
            {
                new(0.0, 0.0),
                new(1.0, 0.0),
                new(0.0, 1.0),
                new(1.0, 1.0),
            },
            Weights: null,
            Domain: null);

        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        foreach (var p in input.Points)
        {
            triangulation.Insert(new Point2<double>(p.X, p.Y));
        }

        triangulation.NumVertices.Should().Be(4);
        triangulation.NumFaces.Should().Be(3); // Outer + 2 inner
        triangulation.NumUndirectedEdges.Should().Be(5);
    }
}
