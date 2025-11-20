using System.Collections;
using System.Collections.Generic;
using Spade.Handles;
using Spade.Primitives;

namespace Spade;

public abstract record Intersection<V, DE, UE, F>
    where V : IHasPosition<double>
    where DE : new()
    where UE : new()
    where F : new()
{
    public sealed record EdgeIntersection(DirectedEdgeHandle<V, DE, UE, F> Edge) : Intersection<V, DE, UE, F>;
    public sealed record VertexIntersection(VertexHandle<V, DE, UE, F> Vertex) : Intersection<V, DE, UE, F>;
    public sealed record EdgeOverlap(DirectedEdgeHandle<V, DE, UE, F> Edge) : Intersection<V, DE, UE, F>;
}

public class LineIntersectionIterator<V, DE, UE, F, L> : IEnumerable<Intersection<V, DE, UE, F>>, IEnumerator<Intersection<V, DE, UE, F>>
    where V : IHasPosition<double>, new()
    where DE : new()
    where UE : new()
    where F : new()
    where L : IHintGenerator<double>, new()
{
    private readonly TriangulationBase<V, DE, UE, F, L> _triangulation;
    private readonly Point2<double> _lineFrom;
    private readonly Point2<double> _lineTo;
    private Intersection<V, DE, UE, F>? _currentIntersection;
    private bool _first = true;
    private bool _useCurrent = false;

    public LineIntersectionIterator(TriangulationBase<V, DE, UE, F, L> triangulation, Point2<double> lineFrom, Point2<double> lineTo)
    {
        _triangulation = triangulation;
        _lineFrom = lineFrom;
        _lineTo = lineTo;
    }

    public static LineIntersectionIterator<V, DE, UE, F, L> NewFromHandles(
        TriangulationBase<V, DE, UE, F, L> triangulation,
        FixedVertexHandle from,
        FixedVertexHandle to)
    {
        var vFrom = triangulation.Vertex(from);
        var vTo = triangulation.Vertex(to);
        var iterator = new LineIntersectionIterator<V, DE, UE, F, L>(triangulation, vFrom.Data.Position, vTo.Data.Position);
        iterator._currentIntersection = new Intersection<V, DE, UE, F>.VertexIntersection(vFrom);
        iterator._first = false;
        iterator._useCurrent = true;
        return iterator;
    }

    public Intersection<V, DE, UE, F> Current => _currentIntersection!;

    object IEnumerator.Current => Current;

    public void Dispose() { }

    public bool MoveNext()
    {
        if (_useCurrent)
        {
            _useCurrent = false;
            return true;
        }

        if (_first)
        {
            _currentIntersection = GetFirstIntersection();
            _first = false;
        }
        else
        {
            _currentIntersection = GetNextIntersection();
        }
        return _currentIntersection != null;
    }

    public void Reset()
    {
        _first = true;
        _currentIntersection = null;
    }

    public IEnumerator<Intersection<V, DE, UE, F>> GetEnumerator() => this;

    IEnumerator IEnumerable.GetEnumerator() => this;

    private Intersection<V, DE, UE, F>? GetFirstIntersection()
    {
        var location = _triangulation.LocateWithHintOptionCore(_lineFrom, null);
        
        switch (location)
        {
            case PositionInTriangulation.OutsideOfConvexHull outside:
                // TODO: Implement logic for starting outside convex hull
                // For now, assume we start inside or on boundary
                throw new NotImplementedException("Starting outside convex hull not fully implemented");
                
            case PositionInTriangulation.OnFace onFace:
                var face = _triangulation.Face(onFace.Face);
                var startEdge = face.AdjacentEdge();
                if (startEdge == null) return null;

                var current = startEdge.Value;
                do
                {
                    var curFrom = current.From().Data.Position;
                    var curTo = current.To().Data.Position;
                    
                    if (MathUtils.IntersectsEdgeNonCollinear(_lineFrom, _lineTo, curFrom, curTo))
                    {
                        if (MathUtils.SideQuery(_lineFrom, _lineTo, curFrom).IsOnLine)
                        {
                            return new Intersection<V, DE, UE, F>.VertexIntersection(current.From());
                        }
                        else if (MathUtils.SideQuery(_lineFrom, _lineTo, curTo).IsOnLine)
                        {
                            return new Intersection<V, DE, UE, F>.VertexIntersection(current.To());
                        }
                        return new Intersection<V, DE, UE, F>.EdgeIntersection(current.Rev());
                    }
                    current = current.Next();
                } while (current.Handle != startEdge.Value.Handle);
                return null;

            case PositionInTriangulation.OnVertex onVertex:
                return new Intersection<V, DE, UE, F>.VertexIntersection(_triangulation.Vertex(onVertex.Vertex));

            case PositionInTriangulation.OnEdge onEdge:
                var edge = _triangulation.DirectedEdge(onEdge.Edge);
                var edgeFrom = edge.From().Data.Position;
                var edgeTo = edge.To().Data.Position;
                
                var fromQuery = MathUtils.SideQuery(_lineFrom, _lineTo, edgeFrom);
                var toQuery = MathUtils.SideQuery(_lineFrom, _lineTo, edgeTo);
                
                if (fromQuery.IsOnLine && toQuery.IsOnLine)
                {
                    var distTo = (edgeTo.Sub(_lineTo)).Length2();
                    var distFrom = (edgeFrom.Sub(_lineTo)).Length2();
                    if (distTo < distFrom)
                    {
                        return new Intersection<V, DE, UE, F>.EdgeOverlap(edge);
                    }
                    else
                    {
                        return new Intersection<V, DE, UE, F>.EdgeOverlap(edge.Rev());
                    }
                }
                else
                {
                    var edgeQuery = edge.SideQuery(_lineTo);
                    if (edgeQuery.IsOnLeftSide)
                    {
                        return new Intersection<V, DE, UE, F>.EdgeIntersection(edge);
                    }
                    else
                    {
                        return new Intersection<V, DE, UE, F>.EdgeIntersection(edge.Rev());
                    }
                }

            case PositionInTriangulation.NoTriangulation:
                if (_triangulation.NumVertices > 0)
                {
                    // Check if single vertex is on line
                    // ...
                }
                return null;
                
            default:
                return null;
        }
    }

    private Intersection<V, DE, UE, F>? GetNextIntersection()
    {
        switch (_currentIntersection)
        {
            case Intersection<V, DE, UE, F>.EdgeIntersection edgeInt:
                return TraceDirectionOutOfEdge(edgeInt.Edge);
            case Intersection<V, DE, UE, F>.VertexIntersection vertexInt:
                if (vertexInt.Vertex.Data.Position == _lineTo) return null;
                return TraceDirectionOutOfVertex(vertexInt.Vertex);
            case Intersection<V, DE, UE, F>.EdgeOverlap edgeOverlap:
                if (_lineFrom == _lineTo) return null;
                var proj = MathUtils.ProjectPoint(_lineFrom, _lineTo, edgeOverlap.Edge.To().Data.Position);
                if (proj.IsOnEdge)
                {
                    return new Intersection<V, DE, UE, F>.VertexIntersection(edgeOverlap.Edge.To());
                }
                return null;
            default:
                return null;
        }
    }

    private Intersection<V, DE, UE, F>? TraceDirectionOutOfEdge(DirectedEdgeHandle<V, DE, UE, F> edge)
    {
        // edge.SideQuery(_lineTo) must be Left or OnLine
        
        var ePrev = edge.Face().IsOuter ? null : (DirectedEdgeHandle<V, DE, UE, F>?)edge.Prev();
        if (ePrev == null) return null; // Convex hull reached

        var oNext = edge.Next();
        
        var ePrevInter = MathUtils.IntersectsEdgeNonCollinear(_lineFrom, _lineTo, ePrev.Value.From().Data.Position, ePrev.Value.To().Data.Position);
        var oNextInter = MathUtils.IntersectsEdgeNonCollinear(_lineFrom, _lineTo, oNext.From().Data.Position, oNext.To().Data.Position);
        
        if (ePrevInter && oNextInter)
        {
            return new Intersection<V, DE, UE, F>.VertexIntersection(ePrev.Value.From());
        }
        if (ePrevInter)
        {
            return new Intersection<V, DE, UE, F>.EdgeIntersection(ePrev.Value.Rev());
        }
        if (oNextInter)
        {
            return new Intersection<V, DE, UE, F>.EdgeIntersection(oNext.Rev());
        }
        return null;
    }

    private Intersection<V, DE, UE, F>? TraceDirectionOutOfVertex(VertexHandle<V, DE, UE, F> vertex)
    {
        var startEdge = vertex.OutEdge();
        if (startEdge == null) return null;

        var currentEdge = startEdge.Value;
        var currentQuery = currentEdge.SideQuery(_lineTo);
        var iterateCcw = currentQuery.IsOnLeftSide;

        var loopCounter = 1000; // Safety break
        while (loopCounter-- > 0)
        {
            if (currentQuery.IsOnLine && !MathUtils.ProjectPoint(currentEdge.From().Data.Position, currentEdge.To().Data.Position, _lineTo).IsBeforeEdge)
            {
                return new Intersection<V, DE, UE, F>.EdgeOverlap(currentEdge);
            }

            var next = iterateCcw ? currentEdge.CCW() : currentEdge.CW();
            var nextQuery = next.SideQuery(_lineTo);

            if (nextQuery.IsOnLine && !MathUtils.ProjectPoint(next.From().Data.Position, next.To().Data.Position, _lineTo).IsBeforeEdge)
            {
                return new Intersection<V, DE, UE, F>.EdgeOverlap(next);
            }

            var faceBetween = iterateCcw ? currentEdge.Face() : next.Face();
            if (faceBetween.IsOuter)
            {
                return null; // Convex hull
            }

            if (iterateCcw == nextQuery.IsOnRightSide)
            {
                var segmentEdge = iterateCcw ? currentEdge.Next() : currentEdge.Rev().Prev();
                return new Intersection<V, DE, UE, F>.EdgeIntersection(segmentEdge.Rev());
            }

            currentQuery = nextQuery;
            currentEdge = next;
            
            if (currentEdge.Handle == startEdge.Value.Handle) break;
        }
        return null;
    }
}
