using System;
using System.Collections.Generic;
using Spade.Handles;
using Spade.Primitives;

namespace Spade;

internal interface IDistanceMetric
{
    bool IsEdgeInside(Point2<double> p0, Point2<double> p1);

    double DistanceToPoint(Point2<double> point);

    bool IsPointInside(Point2<double> point);
}

internal sealed class CircleMetric : IDistanceMetric
{
    private readonly Point2<double> _center;
    private readonly double _radiusSquared;

    internal CircleMetric(Point2<double> center, double radiusSquared)
    {
        if (radiusSquared < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(radiusSquared), "Radius squared must be non-negative.");
        }

        _center = center;
        _radiusSquared = radiusSquared;
    }

    public bool IsEdgeInside(Point2<double> p0, Point2<double> p1)
    {
        var d2 = MathUtils.Distance2(p0, p1, _center);
        return d2 <= _radiusSquared;
    }

    public double DistanceToPoint(Point2<double> point)
    {
        return _center.Distance2(point) - _radiusSquared;
    }

    public bool IsPointInside(Point2<double> point)
    {
        return _center.Distance2(point) <= _radiusSquared;
    }
}

internal sealed class RectangleMetric : IDistanceMetric
{
    private readonly Point2<double> _lower;
    private readonly Point2<double> _upper;

    internal RectangleMetric(Point2<double> lower, Point2<double> upper)
    {
        _lower = lower;
        _upper = upper;
    }

    public bool IsEdgeInside(Point2<double> from, Point2<double> to)
    {
        if (IsPointInside(from) || IsPointInside(to))
        {
            return true;
        }

        if (_lower == _upper)
        {
            var query = MathUtils.SideQuery(from, to, _lower);
            return query.IsOnLine;
        }

        foreach (var edge in Edges())
        {
            var v0 = edge[0];
            var v1 = edge[1];
            var (s0, s1) = GetIntersectionParameters(v0, v1, from, to);
            if (double.IsInfinity(s0))
            {
                var side = MathUtils.SideQuery(from, to, v0);
                if (!side.IsOnLine)
                {
                    continue;
                }

                var proj1 = MathUtils.ProjectPoint(from, to, v0);
                var proj2 = MathUtils.ProjectPoint(from, to, v1);
                if (proj1.IsOnEdge || proj2.IsOnEdge)
                {
                    return true;
                }
            }
            else if (s0 >= 0.0 && s0 <= 1.0 && s1 >= 0.0 && s1 <= 1.0)
            {
                return true;
            }
        }

        return false;
    }

    public double DistanceToPoint(Point2<double> point)
    {
        if (_lower == _upper)
        {
            return point.Distance2(_lower);
        }

        if (IsPointInside(point))
        {
            return 0.0;
        }

        var edges = Edges();
        var d0 = MathUtils.Distance2(edges[0][0], edges[0][1], point);
        var d1 = MathUtils.Distance2(edges[1][0], edges[1][1], point);
        var d2 = MathUtils.Distance2(edges[2][0], edges[2][1], point);
        var d3 = MathUtils.Distance2(edges[3][0], edges[3][1], point);

        return Math.Min(Math.Min(d0, d1), Math.Min(d2, d3));
    }

    public bool IsPointInside(Point2<double> point)
    {
        var insideLower = point.AllComponentWise(_lower, (a, b) => a >= b);
        var insideUpper = point.AllComponentWise(_upper, (a, b) => a <= b);
        return insideLower && insideUpper;
    }

    private Point2<double>[][] Edges()
    {
        var lower = _lower;
        var upper = _upper;

        var v0 = lower;
        var v1 = new Point2<double>(lower.X, upper.Y);
        var v2 = upper;
        var v3 = new Point2<double>(upper.X, lower.Y);

        return new[]
        {
            new[] { v0, v1 },
            new[] { v1, v2 },
            new[] { v2, v3 },
            new[] { v3, v0 }
        };
    }

    private static (double s0, double s1) GetIntersectionParameters(
        Point2<double> v0,
        Point2<double> v1,
        Point2<double> e0,
        Point2<double> e1)
    {
        var x4 = v1.X;
        var x3 = v0.X;
        var x2 = e1.X;
        var x1 = e0.X;
        var y4 = v1.Y;
        var y3 = v0.Y;
        var y2 = e1.Y;
        var y1 = e0.Y;

        var divisor = (y4 - y3) * (x2 - x1) - (x4 - x3) * (y2 - y1);
        if (divisor == 0.0)
        {
            return (double.PositiveInfinity, double.PositiveInfinity);
        }

        var s0 = ((x2 - x1) * (y1 - y3) - (y2 - y1) * (x1 - x3)) / divisor;
        var s1 = ((x4 - x3) * (y1 - y3) - (y4 - y3) * (x1 - x3)) / divisor;
        return (s0, s1);
    }
}

public static class TriangulationShapeExtensions
{
    public static IEnumerable<UndirectedEdgeHandle<V, DE, UE, F>> GetEdgesInRectangle<V, DE, UE, F, L>(
        this TriangulationBase<V, DE, UE, F, L> triangulation,
        Point2<double> lower,
        Point2<double> upper)
        where V : IHasPosition<double>, new()
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        var metric = new RectangleMetric(lower, upper);

        foreach (var edge in triangulation.UndirectedEdges())
        {
            var index = edge.Handle.Index;
            var directed = triangulation.DirectedEdge(new FixedDirectedEdgeHandle(index * 2));
            var from = ((IHasPosition<double>)directed.From().Data).Position;
            var to = ((IHasPosition<double>)directed.To().Data).Position;
            if (metric.IsEdgeInside(from, to))
            {
                yield return edge;
            }
        }
    }

    public static IEnumerable<UndirectedEdgeHandle<V, DE, UE, F>> GetEdgesInCircle<V, DE, UE, F, L>(
        this TriangulationBase<V, DE, UE, F, L> triangulation,
        Point2<double> center,
        double radiusSquared)
        where V : IHasPosition<double>, new()
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        var metric = new CircleMetric(center, radiusSquared);

        foreach (var edge in triangulation.UndirectedEdges())
        {
            var index = edge.Handle.Index;
            var directed = triangulation.DirectedEdge(new FixedDirectedEdgeHandle(index * 2));
            var from = ((IHasPosition<double>)directed.From().Data).Position;
            var to = ((IHasPosition<double>)directed.To().Data).Position;
            if (metric.IsEdgeInside(from, to))
            {
                yield return edge;
            }
        }
    }

    public static IEnumerable<VertexHandle<V, DE, UE, F>> GetVerticesInRectangle<V, DE, UE, F, L>(
        this TriangulationBase<V, DE, UE, F, L> triangulation,
        Point2<double> lower,
        Point2<double> upper)
        where V : IHasPosition<double>, new()
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        var metric = new RectangleMetric(lower, upper);

        foreach (var vertex in triangulation.Vertices())
        {
            var pos = ((IHasPosition<double>)vertex.Data).Position;
            if (metric.IsPointInside(pos))
            {
                yield return vertex;
            }
        }
    }

    public static IEnumerable<VertexHandle<V, DE, UE, F>> GetVerticesInCircle<V, DE, UE, F, L>(
        this TriangulationBase<V, DE, UE, F, L> triangulation,
        Point2<double> center,
        double radiusSquared)
        where V : IHasPosition<double>, new()
        where DE : new()
        where UE : new()
        where F : new()
        where L : IHintGenerator<double>, new()
    {
        var metric = new CircleMetric(center, radiusSquared);

        foreach (var vertex in triangulation.Vertices())
        {
            var pos = ((IHasPosition<double>)vertex.Data).Position;
            if (metric.IsPointInside(pos))
            {
                yield return vertex;
            }
        }
    }
}

