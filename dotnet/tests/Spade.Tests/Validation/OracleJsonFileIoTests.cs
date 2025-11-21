using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Xunit;

namespace Spade.Tests.Validation;

public class OracleJsonFileIoTests
{
    [Fact]
    public void Input_CanBeWrittenToAndReadFromFile()
    {
        var input = new OracleInput(
            Points: new List<OraclePoint>
            {
                new(0.1, 0.2),
                new(0.5, 0.8),
            },
            Weights: new List<double> { 0.0, 1.5 },
            Domain: null);

        var tempPath = Path.GetTempFileName();
        try
        {
            OracleJson.WriteInputToFile(input, tempPath);
            var roundTripped = OracleJson.ReadInputFromFile(tempPath);

            roundTripped.Should().BeEquivalentTo(input);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
