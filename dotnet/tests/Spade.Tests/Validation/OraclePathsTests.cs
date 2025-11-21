using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Xunit;

namespace Spade.Tests.Validation;

public class OraclePathsTests
{
    [Fact]
    public void WriteInputToOracleInputs_WritesFileUnderExpectedStructure()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);

        try
        {
            var input = new OracleInput(
                Points: new List<OraclePoint>
                {
                    new(0.1, 0.2),
                    new(0.5, 0.8),
                },
                Weights: null,
                Domain: null);

            var path = OraclePaths.WriteInputToOracleInputs(input, repoRoot, "simple_case");

            File.Exists(path).Should().BeTrue();

            var roundTripped = OracleJson.ReadInputFromFile(path);
            roundTripped.Should().BeEquivalentTo(input);

            // Basic structure check: path should contain the oracle_inputs segment.
            path.Replace('\\', '/').Should().Contain("/oracle_inputs/");
        }
        finally
        {
            if (Directory.Exists(repoRoot))
            {
                Directory.Delete(repoRoot, recursive: true);
            }
        }
    }
}
