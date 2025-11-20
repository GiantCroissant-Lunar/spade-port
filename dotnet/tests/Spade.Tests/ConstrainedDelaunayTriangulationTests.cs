using FluentAssertions;
using Spade.Primitives;
using Xunit;

namespace Spade.Tests;

public class ConstrainedDelaunayTriangulationTests
{
    [Fact]
    public void TestAddConstraint()
    {
        var cdt = new ConstrainedDelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>>();
        var v0 = cdt.Insert(new Point2<double>(0.0, 0.0));
        var v1 = cdt.Insert(new Point2<double>(1.0, 0.0));
        var v2 = cdt.Insert(new Point2<double>(0.0, 1.0));
        var v3 = cdt.Insert(new Point2<double>(1.0, 1.0));
        
        // Initially Delaunay: diagonal is v1-v2 (0,1)-(1,0) or v0-v3 (0,0)-(1,1)?
        // Square (0,0)-(1,0)-(1,1)-(0,1).
        // v0(0,0), v1(1,0), v2(0,1), v3(1,1).
        // v0, v1, v2 form a triangle. v3 is inserted.
        // v3 is (1,1).
        // If v0, v1, v2 are CCW: (0,0)->(1,0)->(0,1).
        // v3 is outside v1-v2 edge?
        // v1-v2 is (1,0)-(0,1). Midpoint (0.5, 0.5).
        // v3 (1,1) is on right side of v1->v2?
        // (0-1)*(1-0) - (1-0)*(1-1) = -1 - 0 = -1. Right side.
        // So v3 is outside.
        // It will form v1-v3-v2.
        // So diagonal is v1-v2.
        
        // We want to force constraint v0-v3.
        
        cdt.AddConstraint(v0, v3).Should().BeTrue();
        
        cdt.NumConstraints.Should().Be(1);
        
        // Check if edge v0-v3 exists
        // We don't have GetEdgeFromNeighbors yet.
        // But we can check CanAddConstraint(v1, v2) which should be false if it intersects v0-v3.
        
        cdt.CanAddConstraint(v1, v2).Should().BeFalse();
    }
}
