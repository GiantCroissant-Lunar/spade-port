using System;
using System.Collections.Generic;
using Spade.DCEL;
using Spade.Handles;
using Spade.Primitives;

namespace Spade;

public sealed class NaturalNeighborInterpolator<V, DE, UE, F, L>
    where V : IHasPosition<double>, new()
    where DE : new()
    where UE : new()
    where F : new()
    where L : IHintGenerator<double>, new()
{
    private readonly DelaunayTriangulation<V, DE, UE, F, L> _triangulation;
    private readonly List<FixedDirectedEdgeHandle> _inspectEdgesBuffer = new();
    private readonly List<FixedDirectedEdgeHandle> _naturalNeighborEdges = new();
    private readonly List<Point2<double>> _insertCellBuffer = new();
    private readonly List<(FixedVertexHandle Vertex, double Weight)> _weightBuffer = new();

    internal NaturalNeighborInterpolator(DelaunayTriangulation<V, DE, UE, F, L> triangulation)
    {
        _triangulation = triangulation ?? throw new ArgumentNullException(nameof(triangulation));
    }

    public void GetWeights(Point2<double> position, IList<(FixedVertexHandle Vertex, double Weight)> result)
    {
        GetWeightsInternal(position, result);
    }

    private PositionInTriangulation GetWeightsInternal(Point2<double> position, IList<(FixedVertexHandle Vertex, double Weight)> result)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));

        result.Clear();
        _naturalNeighborEdges.Clear();

        var location = GetNaturalNeighborEdges(
            _triangulation,
            _inspectEdgesBuffer,
            position,
            _naturalNeighborEdges);

        GetNaturalNeighborWeights(
            position,
            _naturalNeighborEdges,
            _insertCellBuffer,
            result);

        return location;
    }

    public double? Interpolate(Func<VertexHandle<V, DE, UE, F>, double> selector, Point2<double> position)
    {
        if (selector == null) throw new ArgumentNullException(nameof(selector));

        _weightBuffer.Clear();
        PositionInTriangulation location = null;
        try
        {
            location = GetWeightsInternal(position, _weightBuffer);
        }
        catch (InvalidOperationException)
        {
            // If the natural neighbor construction fails (e.g., due to an unexpected
            // encounter with the outer face), fall back to barycentric interpolation.
            var barycentric = _triangulation.Barycentric();
            return barycentric.Interpolate(selector, position);
        }
        if (_weightBuffer.Count == 0)
        {
            return GetBarycentricFallback(position, selector, location);
        }
        var sum = 0.0;
        var hasFiniteContribution = false;
        foreach (var (vertexHandle, weight) in _weightBuffer)
        {
            if (double.IsNaN(weight) || double.IsInfinity(weight))
            {
                continue;
            }

            var vertex = _triangulation.Vertex(vertexHandle);
            var value = selector(vertex);
            sum += value * weight;
            hasFiniteContribution = true;
        }

        if (!hasFiniteContribution || double.IsNaN(sum) || double.IsInfinity(sum))
        {
            // As a robustness fallback, use barycentric interpolation at this query point.
            var barycentric = _triangulation.Barycentric();
            return barycentric.Interpolate(selector, position);
        }

        return sum;
    }

    public double? InterpolateGradient(
        Func<VertexHandle<V, DE, UE, F>, double> value,
        Func<VertexHandle<V, DE, UE, F>, Point2<double>> gradient,
        double flatness,
        Point2<double> position)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        if (gradient == null) throw new ArgumentNullException(nameof(gradient));

        _weightBuffer.Clear();
        PositionInTriangulation location = null;
        try
        {
            location = GetWeightsInternal(position, _weightBuffer);
        }
        catch (InvalidOperationException)
        {
            // If natural neighbor construction fails (e.g. due to encountering
            // the outer face unexpectedly), fall back to barycentric interpolation.
            var barycentric = _triangulation.Barycentric();
            return barycentric.Interpolate(value, position);
        }

        if (_weightBuffer.Count == 0)
        {
            return GetBarycentricFallback(position, value, location);
        }

        var sumC0 = 0.0;
        var sumC1 = 0.0;
        var sumC1Weights = 0.0;
        var alpha = 0.0;
        var beta = 0.0;
        var hasFiniteContribution = false;

        foreach (var (vertexHandle, weight) in _weightBuffer)
        {
            if (double.IsNaN(weight) || double.IsInfinity(weight))
            {
                continue;
            }

            var handle = _triangulation.Vertex(vertexHandle);
            var pos = ((IHasPosition<double>)handle.Data).Position;
            var h = value(handle);
            var diff = position.Sub(pos);
            var r2 = diff.Length2();

            if (r2 == 0.0)
            {
                return h;
            }

            var r = Math.Pow(r2, flatness);
            var c1Weight = weight / r;
            var grad = gradient(handle);
            if (double.IsNaN(grad.X) || double.IsNaN(grad.Y) || double.IsInfinity(grad.X) || double.IsInfinity(grad.Y))
            {
                grad = new Point2<double>(0.0, 0.0);
            }
            var zeta = h + diff.Dot(grad);

            alpha += c1Weight * r2;
            beta += weight * r2;
            sumC1Weights += c1Weight;
            sumC1 += zeta * c1Weight;
            sumC0 += h * weight;
            hasFiniteContribution = true;
        }

        if (!hasFiniteContribution)
        {
            var barycentric = _triangulation.Barycentric();
            return barycentric.Interpolate(value, position);
        }

        if (sumC1Weights == 0.0)
        {
            return sumC0;
        }

        alpha /= sumC1Weights;
        sumC1 /= sumC1Weights;

        var denom = alpha + beta;
        if (denom == 0.0 || double.IsNaN(denom) || double.IsInfinity(denom))
        {
            return sumC0;
        }

        var result = (alpha * sumC0 + beta * sumC1) / denom;
        if (double.IsNaN(result) || double.IsInfinity(result))
        {
            return sumC0;
        }

        return result;
    }

    public Point2<double> EstimateGradient(
        VertexHandle<V, DE, UE, F> vertex,
        Func<VertexHandle<V, DE, UE, F>, double> value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));

        var vPos2D = ((IHasPosition<double>)vertex.Data).Position;
        var vz = value(vertex);

        var vx = vPos2D.X;
        var vy = vPos2D.Y;

        var neighborPositions = new List<(double X, double Y, double Z)>();

        var startEdge = vertex.OutEdge();
        if (startEdge != null)
        {
            var current = startEdge.Value;
            var visitedEdges = new HashSet<int>();

            while (true)
            {
                if (!visitedEdges.Add(current.Handle.Index))
                {
                    break;
                }

                var to = current.To();
                var pos = ((IHasPosition<double>)to.Data).Position;
                var h = value(to);
                neighborPositions.Add((pos.X, pos.Y, h));

                current = current.CCW();
                if (current.Handle.Index == startEdge.Value.Handle.Index)
                {
                    break;
                }
            }
        }

        if (neighborPositions.Count == 0)
        {
            return new Point2<double>(0.0, 0.0);
        }

        var finalNx = 0.0;
        var finalNy = 0.0;
        var finalNz = 0.0;

        for (int i = 0; i < neighborPositions.Count; i++)
        {
            var p0 = neighborPositions[i];
            var p1 = neighborPositions[(i + 1) % neighborPositions.Count];

            var d0x = p0.X - vx;
            var d0y = p0.Y - vy;
            var d0z = p0.Z - vz;

            var d1x = p1.X - vx;
            var d1y = p1.Y - vy;
            var d1z = p1.Z - vz;

            var nx = d0y * d1z - d0z * d1y;
            var ny = d0z * d1x - d0x * d1z;
            var nz = d0x * d1y - d0y * d1x;

            if (nz > 0.0)
            {
                finalNx += nx;
                finalNy += ny;
                finalNz += nz;
            }
        }

        if (finalNz != 0.0)
        {
            var gx = -finalNx / finalNz;
            var gy = -finalNy / finalNz;
            return new Point2<double>(gx, gy);
        }

        return new Point2<double>(0.0, 0.0);
    }

    public Func<VertexHandle<V, DE, UE, F>, Point2<double>> EstimateGradients(
        Func<VertexHandle<V, DE, UE, F>, double> value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));

        var gradients = new List<Point2<double>>();
        foreach (var v in _triangulation.Vertices())
        {
            gradients.Add(EstimateGradient(v, value));
        }

        return v => gradients[v.Handle.Index];
    }

    private double? GetBarycentricFallback(
        Point2<double> position,
        Func<VertexHandle<V, DE, UE, F>, double> selector,
        PositionInTriangulation knownLocation = null)
    {
        // Check Bounding Box
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var v in _triangulation.Vertices())
        {
            var p = ((IHasPosition<double>)v.Data).Position;
            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;
        }

        bool insideBox = position.X >= minX && position.X <= maxX && position.Y >= minY && position.Y <= maxY;

        if (!insideBox)
        {
            return null;
        }

        // If we can reliably classify the point as outside the convex hull,
        // do not attempt a barycentric fallback: natural neighbor semantics
        // for truly exterior points should remain "null".
        if (_triangulation.TryIsInsideConvexHull(position, out var isInsideHull) && !isInsideHull)
        {
            return null;
        }

        // Robustness fallback:
        var location = knownLocation ?? _triangulation.LocateWithHintOptionCore(position, null);
        if (location is PositionInTriangulation.OutsideOfConvexHull outside)
        {
            var edge = _triangulation.DirectedEdge(outside.Edge);
            var face = edge.Face();
            if (face.IsOuter)
            {
                edge = edge.Rev();
                face = edge.Face();
            }

            if (!face.IsOuter)
            {
                var start = face.AdjacentEdge();
                if (start != null)
                {
                    var e1 = start.Value;
                    var e0 = e1.Prev();
                    var e2 = e1.Next();

                    var v0 = ((IHasPosition<double>)e0.From().Data).Position;
                    var v1 = ((IHasPosition<double>)e1.From().Data).Position;
                    var v2 = ((IHasPosition<double>)e2.From().Data).Position;

                    var (l0, l1, l2) = MathUtils.BarycentricCoordinates(v0, v1, v2, position);

                    var val0 = selector(e0.From());
                    var val1 = selector(e1.From());
                    var val2 = selector(e2.From());

                    return val0 * l0 + val1 * l1 + val2 * l2;
                }
            }
        }

        return null;
    }

    private static PositionInTriangulation GetNaturalNeighborEdges(
        TriangulationBase<V, DE, UE, F, L> triangulation,
        List<FixedDirectedEdgeHandle> inspectBuffer,
        Point2<double> position,
        List<FixedDirectedEdgeHandle> result)
    {
        inspectBuffer.Clear();
        result.Clear();

        var location = triangulation.LocateWithHintOptionCore(position, null);
        switch (location)
        {
            case PositionInTriangulation.OnFace onFace:
                {
                    var face = triangulation.Face(onFace.Face);
                    if (face.IsOuter)
                    {
                        break;
                    }

                    var edge = face.AdjacentEdge();
                    if (edge == null)
                    {
                        break;
                    }

                    var e1 = edge.Value;
                    var e0 = e1.Prev();
                    var e2 = e1.Next();

                    var edges = new[]
                    {
                    e2.Rev().Handle,
                    e1.Rev().Handle,
                    e0.Rev().Handle
                };

                    foreach (var e in edges)
                    {
                        InspectFlips(triangulation, result, inspectBuffer, e, position);
                    }

                    if (result.Count == 0)
                    {
                        throw new InvalidOperationException("Natural neighbor search failed for interior point (OnFace).");
                    }

                    break;
                }

            case PositionInTriangulation.OnEdge onEdge:
                {
                    var edge = triangulation.DirectedEdge(onEdge.Edge);
                    var face = edge.Face();
                    var revFace = edge.Rev().Face();
                    var isHullEdge = face.IsOuter ^ revFace.IsOuter;

                    if (isHullEdge)
                    {
                        result.Add(edge.Handle);
                        result.Add(edge.Handle.Rev());
                        result.Reverse();
                        return location;
                    }

                    var edges = new[]
                    {
                    edge.Handle,
                    edge.Rev().Handle
                };

                    foreach (var e in edges)
                    {
                        InspectFlips(triangulation, result, inspectBuffer, e, position);
                    }

                    if (result.Count == 0)
                    {
                        throw new InvalidOperationException("Natural neighbor search failed for interior point (OnEdge).");
                    }

                    break;
                }

            case PositionInTriangulation.OnVertex onVertex:
                {
                    var vertex = triangulation.Vertex(onVertex.Vertex);
                    var outEdge = vertex.OutEdge();
                    if (outEdge == null)
                    {
                        throw new InvalidOperationException("Vertex has no outgoing edge.");
                    }
                    var handle = outEdge.Value.Handle;
                    result.Add(handle);
                    result.Reverse();
                    return location;
                }

            case PositionInTriangulation.OutsideOfConvexHull outside:
                {
                    break;
                }

            default:
                break;
        }

        result.Reverse();
        return location;
    }

    private static void InspectFlips(
        TriangulationBase<V, DE, UE, F, L> triangulation,
        List<FixedDirectedEdgeHandle> result,
        List<FixedDirectedEdgeHandle> buffer,
        FixedDirectedEdgeHandle edgeToValidate,
        Point2<double> position)
    {
        buffer.Clear();
        buffer.Add(edgeToValidate);
        var visited = new HashSet<int>();
        var maxSteps = triangulation.NumDirectedEdges * 4;

        while (buffer.Count > 0 && maxSteps-- > 0)
        {
            var current = buffer[buffer.Count - 1];
            buffer.RemoveAt(buffer.Count - 1);

            if (!visited.Add(current.Index))
            {
                continue;
            }

            var edge = triangulation.DirectedEdge(current);

            var v2 = edge.OppositeVertex();
            var v1 = edge.From();
            var v0Pos = ((IHasPosition<double>)edge.To().Data).Position;

            var shouldFlip = false;

            if (v2 != null)
            {
                var v2Pos = ((IHasPosition<double>)v2.Value.Data).Position;
                var v1Pos = ((IHasPosition<double>)v1.Data).Position;

                shouldFlip = MathUtils.ContainedInCircumference(
                    v2Pos,
                    v1Pos,
                    v0Pos,
                    position);

                if (shouldFlip)
                {
                    var e1 = edge.Next().Handle.Rev();
                    var e2 = edge.Prev().Handle.Rev();

                    buffer.Add(e1);
                    buffer.Add(e2);
                }
            }

            if (!shouldFlip)
            {
                result.Add(edge.Handle.Rev());
            }
        }

        // If we exceeded the step budget, conservatively treat any remaining
        // pending edges as non-flipped to guarantee termination.
        if (maxSteps <= 0)
        {
            while (buffer.Count > 0)
            {
                var current = buffer[buffer.Count - 1];
                buffer.RemoveAt(buffer.Count - 1);
                var edge = triangulation.DirectedEdge(current);
                result.Add(edge.Handle.Rev());
            }
        }
    }

    private void GetNaturalNeighborWeights(
        Point2<double> position,
        List<FixedDirectedEdgeHandle> neighborEdges,
        List<Point2<double>> insertionCell,
        IList<(FixedVertexHandle Vertex, double Weight)> result)
    {
        result.Clear();

        if (neighborEdges.Count == 0)
        {
            return;
        }

        if (neighborEdges.Count == 1)
        {
            var edge = _triangulation.DirectedEdge(neighborEdges[0]);
            result.Add((edge.From().Handle, 1.0));
            return;
        }

        if (neighborEdges.Count == 2)
        {
            var e0 = _triangulation.DirectedEdge(neighborEdges[0]);
            var e1 = _triangulation.DirectedEdge(neighborEdges[1]);

            var v0 = e0.From();
            var v1 = e1.From();
            var (w0, w1) = TwoPointInterpolation(v0, v1, position);

            result.Add((v0.Handle, w0));
            result.Add((v1.Handle, w1));
            return;
        }

        insertionCell.Clear();
        for (int i = 0; i < neighborEdges.Count; i++)
        {
            var edge = _triangulation.DirectedEdge(neighborEdges[i]);
            var from = ((IHasPosition<double>)edge.From().Data).Position;
            var to = ((IHasPosition<double>)edge.To().Data).Position;

            var p0 = to.Sub(position);
            var p1 = from.Sub(position);
            var p2 = new Point2<double>(0.0, 0.0);

            var center = MathUtils.Circumcenter(p0, p1, p2);
            insertionCell.Add(center);
        }

        var totalArea = 0.0;

        var lastEdge = _triangulation.DirectedEdge(neighborEdges[neighborEdges.Count - 1]);
        var last = insertionCell[insertionCell.Count - 1];

        for (int i = 0; i < neighborEdges.Count; i++)
        {
            var stopEdge = _triangulation.DirectedEdge(neighborEdges[i]);
            if (stopEdge.Face().IsOuter)
            {
                throw new InvalidOperationException("Natural neighbor edge lies on outer face.");
            }

            var first = insertionCell[i];

            var positiveArea = first.X * last.Y;
            var negativeArea = first.Y * last.X;

            while (true)
            {
                var face = lastEdge.Face();
                if (face.IsOuter)
                {
                    throw new InvalidOperationException("Encountered outer face while traversing natural neighbor polygon.");
                }

                var adjacent = face.AdjacentEdge();
                if (adjacent == null)
                {
                    throw new InvalidOperationException("Face has no adjacent edge.");
                }

                var e1 = adjacent.Value;
                var e0 = e1.Prev();
                var e2 = e1.Next();

                var v0Pos = ((IHasPosition<double>)e0.From().Data).Position.Sub(position);
                var v1Pos = ((IHasPosition<double>)e1.From().Data).Position.Sub(position);
                var v2Pos = ((IHasPosition<double>)e2.From().Data).Position.Sub(position);

                var current = MathUtils.Circumcenter(v0Pos, v1Pos, v2Pos);

                positiveArea += last.X * current.Y;
                negativeArea += last.Y * current.X;

                lastEdge = lastEdge.Next().Rev();
                last = current;

                if (lastEdge.Handle == stopEdge.Handle.Rev())
                {
                    positiveArea += current.X * first.Y;
                    negativeArea += current.Y * first.X;
                    break;
                }
            }

            var polygonArea = positiveArea - negativeArea;

            totalArea += polygonArea;
            result.Add((stopEdge.From().Handle, polygonArea));

            last = first;
            lastEdge = stopEdge;
        }

        if (totalArea == 0.0)
        {
            // If the natural neighbor polygon has zero area but we found edges,
            // it implies a degenerate configuration (e.g. all neighbors collinear).
            // Treat this as a failure so we can fall back to barycentric interpolation.
            if (neighborEdges.Count > 0)
            {
                throw new InvalidOperationException("Natural neighbor polygon has zero area.");
            }
            return;
        }

        for (int i = 0; i < result.Count; i++)
        {
            var entry = result[i];
            result[i] = (entry.Vertex, entry.Weight / totalArea);
        }
    }

    private static (double W0, double W1) TwoPointInterpolation(
        VertexHandle<V, DE, UE, F> v0,
        VertexHandle<V, DE, UE, F> v1,
        Point2<double> position)
    {
        var p0 = ((IHasPosition<double>)v0.Data).Position;
        var p1 = ((IHasPosition<double>)v1.Data).Position;
        var projection = MathUtils.ProjectPoint(p0, p1, position);
        var rel = projection.RelativePosition();

        var w1 = rel;
        var w0 = 1.0 - w1;
        return (w0, w1);
    }
}
