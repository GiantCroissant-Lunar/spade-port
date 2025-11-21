using System;
using System.Collections.Generic;
using Spade.Primitives;

namespace Spade.Advanced.Voronoi;

/// <summary>
/// Convex polygon used as a clipping domain for Voronoi diagrams.
/// Vertices are expected to be provided in counter-clockwise order.
/// </summary>
public sealed class ClipPolygon
{
    private readonly List<Point2<double>> _vertices;

    public IReadOnlyList<Point2<double>> Vertices => _vertices;

    public ClipPolygon(IReadOnlyList<Point2<double>> vertices)
    {
        if (vertices is null)
            throw new ArgumentNullException(nameof(vertices));
        if (vertices.Count < 3)
            throw new ArgumentException("Clip polygon must have at least 3 vertices.", nameof(vertices));

        _vertices = new List<Point2<double>>(vertices.Count);
        foreach (var v in vertices)
        {
            _vertices.Add(v);
        }
    }
}
