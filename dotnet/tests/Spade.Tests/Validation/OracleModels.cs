using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spade.Tests.Validation;

internal sealed record OraclePoint(
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y);

internal sealed record OracleDomainPolygon(
    [property: JsonPropertyName("vertices")] IReadOnlyList<OraclePoint> Vertices);

internal sealed record OracleDomain(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("polygon")] OracleDomainPolygon? Polygon);

internal sealed record OracleInput(
    [property: JsonPropertyName("points")] IReadOnlyList<OraclePoint> Points,
    [property: JsonPropertyName("weights")] IReadOnlyList<double>? Weights,
    [property: JsonPropertyName("domain")] OracleDomain? Domain);

internal sealed record OracleTriangulationOutput(
    [property: JsonPropertyName("points")] IReadOnlyList<OraclePoint> Points,
    [property: JsonPropertyName("triangles")] IReadOnlyList<int[]> Triangles);

internal sealed record OracleVoronoiCell(
    [property: JsonPropertyName("generatorIndex")] int GeneratorIndex,
    [property: JsonPropertyName("polygon")] IReadOnlyList<OraclePoint> Polygon,
    [property: JsonPropertyName("neighbors")] IReadOnlyList<int> Neighbors);

internal sealed record OracleVoronoiOutput(
    [property: JsonPropertyName("cells")] IReadOnlyList<OracleVoronoiCell> Cells);

internal static class OracleJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static string SerializeInput(OracleInput input)
    {
        return JsonSerializer.Serialize(input, Options);
    }

    public static OracleInput DeserializeInput(string json)
    {
        return JsonSerializer.Deserialize<OracleInput>(json, Options)!;
    }

    public static void WriteInputToFile(OracleInput input, string path)
    {
        var json = SerializeInput(input);
        File.WriteAllText(path, json);
    }

    public static OracleInput ReadInputFromFile(string path)
    {
        var json = File.ReadAllText(path);
        return DeserializeInput(json);
    }

    public static string SerializeTriangulation(OracleTriangulationOutput output)
    {
        return JsonSerializer.Serialize(output, Options);
    }

    public static OracleTriangulationOutput DeserializeTriangulation(string json)
    {
        return JsonSerializer.Deserialize<OracleTriangulationOutput>(json, Options)!;
    }

    public static string SerializeVoronoi(OracleVoronoiOutput output)
    {
        return JsonSerializer.Serialize(output, Options);
    }

    public static OracleVoronoiOutput DeserializeVoronoi(string json)
    {
        return JsonSerializer.Deserialize<OracleVoronoiOutput>(json, Options)!;
    }
}
