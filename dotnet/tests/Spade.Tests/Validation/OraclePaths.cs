using System;
using System.IO;

namespace Spade.Tests.Validation;

internal static class OraclePaths
{
    public static string GetOracleInputsDirectory(string repoRoot)
    {
        return Path.Combine(
            repoRoot,
            "ref-projects",
            "DelaunayTriangulation.jl",
            "test_oracle",
            "oracle_inputs");
    }

    public static string WriteInputToOracleInputs(OracleInput input, string repoRoot, string fileNameWithoutExtension)
    {
        if (string.IsNullOrWhiteSpace(repoRoot)) throw new ArgumentException("Repository root must be provided", nameof(repoRoot));
        if (string.IsNullOrWhiteSpace(fileNameWithoutExtension)) throw new ArgumentException("File name must be provided", nameof(fileNameWithoutExtension));

        var dir = GetOracleInputsDirectory(repoRoot);
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, fileNameWithoutExtension + ".json");
        OracleJson.WriteInputToFile(input, path);
        return path;
    }
}
