using System;
using System.Collections.Generic;
using Spade.Handles;
using Spade.Primitives;

namespace Spade;

public sealed class BarycentricInterpolator<V, DE, UE, F, L>
    where V : IHasPosition<double>, new()
    where DE : new()
    where UE : new()
    where F : new()
    where L : IHintGenerator<double>, new()
{
    private readonly TriangulationBase<V, DE, UE, F, L> _triangulation;
    private readonly List<(FixedVertexHandle Vertex, double Weight)> _buffer;

    internal BarycentricInterpolator(TriangulationBase<V, DE, UE, F, L> triangulation)
    {
        _triangulation = triangulation ?? throw new ArgumentNullException(nameof(triangulation));
        _buffer = new List<(FixedVertexHandle, double)>();
    }

    public void GetWeights(Point2<double> position, IList<(FixedVertexHandle Vertex, double Weight)> result)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));

        result.Clear();
        _buffer.Clear();
        GetWeightsInternal(position, _buffer);

        foreach (var entry in _buffer)
        {
            result.Add(entry);
        }
    }

    public double? Interpolate(Func<VertexHandle<V, DE, UE, F>, double> selector, Point2<double> position)
    {
        if (selector == null) throw new ArgumentNullException(nameof(selector));

        _buffer.Clear();
        GetWeightsInternal(position, _buffer);
        if (_buffer.Count == 0)
        {
            return null;
        }

        var sum = 0.0;
        foreach (var (vertexHandle, weight) in _buffer)
        {
            var vertex = _triangulation.Vertex(vertexHandle);
            sum += selector(vertex) * weight;
        }

        return sum;
    }

    private void GetWeightsInternal(Point2<double> position, List<(FixedVertexHandle Vertex, double Weight)> result)
    {
        result.Clear();

        var location = _triangulation.LocateWithHintOptionCore(position, null);
        switch (location)
        {
            case PositionInTriangulation.OnVertex onVertex:
                result.Add((onVertex.Vertex, 1.0));
                break;

            case PositionInTriangulation.OnEdge onEdge:
                {
                    var edge = _triangulation.DirectedEdge(onEdge.Edge);
                    var v0 = edge.From();
                    var v1 = edge.To();
                    var (w0, w1) = TwoPointInterpolation(v0, v1, position);
                    result.Add((v0.Handle, w0));
                    result.Add((v1.Handle, w1));
                    break;
                }

            case PositionInTriangulation.OnFace onFace:
                {
                    var face = _triangulation.Face(onFace.Face);
                    var edge = face.AdjacentEdge();
                    if (edge == null)
                    {
                        break;
                    }

                    var e1 = edge.Value;
                    var e0 = e1.Prev();
                    var e2 = e1.Next();

                    var v0 = ((IHasPosition<double>)e0.From().Data).Position;
                    var v1 = ((IHasPosition<double>)e1.From().Data).Position;
                    var v2 = ((IHasPosition<double>)e2.From().Data).Position;

                    var (lambda0, lambda1, lambda2) = MathUtils.BarycentricCoordinates(v0, v1, v2, position);

                    result.Add((e0.From().Handle, lambda0));
                    result.Add((e1.From().Handle, lambda1));
                    result.Add((e2.From().Handle, lambda2));
                    break;
                }

            default:
                break;
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
