using System;
using System.Reflection;
using Spade;
using Spade.Primitives;

namespace Spade.Tests.Validation;

internal static class TriangulationLocateOracle
{
    public static PositionInTriangulation BruteForceLocate(
        DelaunayTriangulation<Point2<double>, int, int, int, LastUsedVertexHintGenerator<double>> triangulation,
        Point2<double> target)
    {
        var type = triangulation.GetType();
        var method = type.GetMethod(
            "BruteForceLocate",
            BindingFlags.Instance | BindingFlags.NonPublic);

        var current = type.BaseType;
        while (method == null && current != null)
        {
            method = current.GetMethod(
                "BruteForceLocate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            current = current.BaseType;
        }

        if (method == null)
        {
            throw new InvalidOperationException("BruteForceLocate method not found via reflection.");
        }

        var result = method.Invoke(triangulation, new object[] { target });
        if (result is not PositionInTriangulation pos)
        {
            throw new InvalidOperationException("BruteForceLocate returned unexpected result type.");
        }

        return pos;
    }
}
