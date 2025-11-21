using System.Collections.Generic;
using FluentAssertions;
using Spade.Tests.Validation;
using Xunit;

namespace Spade.Tests.Validation;

public class OracleJsonRoundTripTests
{
    [Fact]
    public void Input_RoundTrips_Through_Json()
    {
        var input = new OracleInput(
            Points: new List<OraclePoint>
            {
                new(0.1, 0.2),
                new(0.5, 0.8),
            },
            Weights: new List<double> { 0.0, 1.5 },
            Domain: new OracleDomain(
                Type: "polygon",
                Polygon: new OracleDomainPolygon(
                    Vertices: new List<OraclePoint>
                    {
                        new(0.0, 0.0),
                        new(1.0, 0.0),
                        new(1.0, 1.0),
                        new(0.0, 1.0),
                    })));

        var json = OracleJson.SerializeInput(input);
        var roundTripped = OracleJson.DeserializeInput(json);

        roundTripped.Should().BeEquivalentTo(input);
    }
}
