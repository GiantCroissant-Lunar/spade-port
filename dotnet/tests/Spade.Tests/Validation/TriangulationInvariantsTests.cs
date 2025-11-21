using FluentAssertions;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests.Validation;

public class TriangulationInvariantsTests
{
    [Fact]
    public void BasicTopology_Holds_ForSimpleTriangulation()
    {
        var triangulation = new DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        triangulation.Insert(new Point2<double>(0.0, 0.0));
        triangulation.Insert(new Point2<double>(1.0, 0.0));
        triangulation.Insert(new Point2<double>(0.0, 1.0));

        TriangulationInvariants.AssertBasicTopology(triangulation);
    }
}
