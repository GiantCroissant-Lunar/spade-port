using FluentAssertions;
using Spade.Refinement;
using Xunit;

namespace Spade.Tests.Refinement;

public class AngleLimitTests
{
    [Fact]
    public void FromDegrees_Zero_DisablesAngleRefinement()
    {
        var limit = AngleLimit.FromDegrees(0.0);

        limit.Radians.Should().Be(0.0);
        limit.Degrees.Should().Be(0.0);
        limit.RadiusToShortestEdgeLimit.Should().Be(double.PositiveInfinity);
    }

    [Fact]
    public void FromDegrees_Thirty_HasExpectedRadiusToShortestEdgeRatio()
    {
        var limit = AngleLimit.FromDegrees(30.0);

        limit.Degrees.Should().BeApproximately(30.0, 1e-9);
        limit.Radians.Should().BeApproximately(Math.PI / 6.0, 1e-12);
        limit.RadiusToShortestEdgeLimit.Should().BeApproximately(1.0, 1e-12);
    }

    [Fact]
    public void FromRadiusToShortestEdgeRatio_InvertsFromDegrees()
    {
        const double degrees = 25.0;
        var fromDegrees = AngleLimit.FromDegrees(degrees);

        var ratio = fromDegrees.RadiusToShortestEdgeLimit;
        var fromRatio = AngleLimit.FromRadiusToShortestEdgeRatio(ratio);

        fromRatio.Degrees.Should().BeApproximately(fromDegrees.Degrees, 1e-9);
        fromRatio.Radians.Should().BeApproximately(fromDegrees.Radians, 1e-12);
        fromRatio.RadiusToShortestEdgeLimit.Should().BeApproximately(ratio, 1e-12);
    }
}
