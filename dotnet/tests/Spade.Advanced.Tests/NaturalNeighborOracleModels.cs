using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spade.Advanced.Tests;

/// <summary>
/// A simple 2D point used in natural neighbor oracle JSON.
/// </summary>
internal sealed record OracleNniPoint(
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y);

/// <summary>
/// A single (point, weight) contribution for a query in the oracle output.
/// </summary>
internal sealed record OracleNniWeightEntry(
    [property: JsonPropertyName("pointIndex")] int PointIndex,
    [property: JsonPropertyName("weight")] double Weight);

/// <summary>
/// Oracle description of a single query: location, weights, and interpolated value.
/// </summary>
internal sealed record OracleNniQueryOutput(
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y,
    [property: JsonPropertyName("weights")] IReadOnlyList<OracleNniWeightEntry> Weights,
    [property: JsonPropertyName("value")] double Value);

/// <summary>
/// Oracle natural neighbor output for a given point set and value field.
/// </summary>
/// <remarks>
/// Expected JSON shape:
/// {
///   "points":  [ { "x": ..., "y": ... }, ... ],
///   "values":  [ v0, v1, ... ],
///   "queries": [
///     {
///       "x": ..., "y": ...,
///       "weights": [ { "pointIndex": 0, "weight": 0.25 }, ... ],
///       "value": ...
///     },
///     ...
///   ]
/// }
/// </remarks>
internal sealed record OracleNniOutput(
    [property: JsonPropertyName("points")] IReadOnlyList<OracleNniPoint> Points,
    [property: JsonPropertyName("values")] IReadOnlyList<double> Values,
    [property: JsonPropertyName("queries")] IReadOnlyList<OracleNniQueryOutput> Queries);

internal static class OracleNniJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static OracleNniOutput ReadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<OracleNniOutput>(json, Options)!;
    }
}
