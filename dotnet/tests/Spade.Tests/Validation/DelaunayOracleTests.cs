using System.Collections.Generic;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests.Validation;

public class DelaunayOracleTests
{
    [Fact]
    public void SimpleInput_SatisfiesBasicTriangulationInvariants()
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

        TriangulationInvariants.AssertBasicTopology(triangulation);
    }
}
