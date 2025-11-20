using Spade.DCEL;
using Spade.Handles;
using Spade.Primitives;

namespace Spade;

public class ConstrainedDelaunayTriangulation<V, DE, UE, F, L> : TriangulationBase<V, DE, CdtEdge<UE>, F, L>
    where V : IHasPosition<double>, new()
    where DE : new()
    where UE : new()
    where F : new()
    where L : IHintGenerator<double>, new()
{
    private int _numConstraints;

    public ConstrainedDelaunayTriangulation() : base()
    {
        _numConstraints = 0;
    }

    public int NumConstraints => _numConstraints;

    protected override bool IsConstraint(FixedUndirectedEdgeHandle edge)
    {
        return _dcel.UndirectedEdge(edge).Data.IsConstraintEdge;
    }

    protected override void HandleLegalEdgeSplit(FixedDirectedEdgeHandle[] handles)
    {
        _numConstraints++;
        foreach (var handle in handles)
        {
            var undirected = handle.AsUndirected();
            if (!IsConstraint(undirected))
            {
                var entry = _dcel.Edges[undirected.Index];
                var data = entry.UndirectedData;
                data.MakeConstraintEdge();
                entry.UndirectedData = data;
                _dcel.Edges[undirected.Index] = entry;
            }
        }
    }

    public bool AddConstraint(FixedVertexHandle from, FixedVertexHandle to)
    {
        var initialNumConstraints = NumConstraints;
        ResolveSplittingConstraintRequest(from, to, null);
        return NumConstraints != initialNumConstraints;
    }

    public bool CanAddConstraint(FixedVertexHandle from, FixedVertexHandle to)
    {
        var iterator = LineIntersectionIterator<V, DE, CdtEdge<UE>, F, L>.NewFromHandles(this, from, to);
        return !ContainsAnyConstraintEdge(iterator);
    }

    public bool IntersectsConstraint(Point2<double> lineFrom, Point2<double> lineTo)
    {
        var iterator = new LineIntersectionIterator<V, DE, CdtEdge<UE>, F, L>(this, lineFrom, lineTo);
        return ContainsAnyConstraintEdge(iterator);
    }

    private bool ContainsAnyConstraintEdge(LineIntersectionIterator<V, DE, CdtEdge<UE>, F, L> iterator)
    {
        foreach (var intersection in iterator)
        {
            if (intersection is Intersection<V, DE, CdtEdge<UE>, F>.EdgeIntersection edgeInt)
            {
                if (IsConstraint(edgeInt.Edge.Handle.AsUndirected())) return true;
            }
        }
        return false;
    }

    private List<FixedDirectedEdgeHandle> ResolveSplittingConstraintRequest(
        FixedVertexHandle from,
        FixedVertexHandle to,
        Func<Point2<double>, V>? vertexConstructor)
    {
        var result = new List<FixedDirectedEdgeHandle>();
        var conflictEdges = new List<FixedDirectedEdgeHandle>();
        
        var iterator = LineIntersectionIterator<V, DE, CdtEdge<UE>, F, L>.NewFromHandles(this, from, to);
        iterator.MoveNext(); // Skip first vertex
        
        while (iterator.MoveNext())
        {
            var intersection = iterator.Current;
            if (intersection is Intersection<V, DE, CdtEdge<UE>, F>.EdgeOverlap edgeOverlap)
            {
                result.Add(edgeOverlap.Edge.Handle);
                from = edgeOverlap.Edge.To().Handle;
            }
            else if (intersection is Intersection<V, DE, CdtEdge<UE>, F>.EdgeIntersection edgeInt)
            {
                if (IsConstraint(edgeInt.Edge.Handle.AsUndirected()))
                {
                    if (vertexConstructor == null)
                    {
                         throw new InvalidOperationException("Constraint intersection detected and no vertex constructor provided (splitting disabled)");
                    }
                    
                    throw new NotImplementedException("Constraint splitting not implemented yet");
                }
                conflictEdges.Add(edgeInt.Edge.Handle);
            }
            else if (intersection is Intersection<V, DE, CdtEdge<UE>, F>.VertexIntersection vertexInt)
            {
                var targetVertex = vertexInt.Vertex.Handle;
                if (conflictEdges.Count > 0)
                {
                    var newEdge = ResolveConflictRegion(new List<FixedDirectedEdgeHandle>(conflictEdges), targetVertex);
                    if (newEdge.HasValue) result.Add(newEdge.Value);
                    conflictEdges.Clear();
                    
                    iterator = LineIntersectionIterator<V, DE, CdtEdge<UE>, F, L>.NewFromHandles(this, targetVertex, to);
                    iterator.MoveNext(); // Skip start
                    from = targetVertex;
                }
            }
        }
        
        foreach (var edge in result)
        {
            MakeConstraintEdge(edge.AsUndirected());
        }
        
        return result;
    }

    private FixedDirectedEdgeHandle? ResolveConflictRegion(List<FixedDirectedEdgeHandle> conflictEdges, FixedVertexHandle targetVertex)
    {
        if (conflictEdges.Count == 0) return null;
        
        var first = conflictEdges[0];
        var firstEdge = DirectedEdge(first);
        
        var firstBorderEdge = firstEdge.Rev().Prev().Handle;
        var lastBorderEdge = firstEdge.Rev().Next().Handle;
        
        foreach (var edge in conflictEdges)
        {
            DcelOperations.FlipCw(_dcel, edge.AsUndirected());
        }
        
        // Temporary constraints for border
        var tempConstraints = new List<FixedUndirectedEdgeHandle>();
        
        void MakeTemp(FixedUndirectedEdgeHandle e)
        {
            if (!IsConstraint(e))
            {
                tempConstraints.Add(e);
                var entry = _dcel.Edges[e.Index];
                var data = entry.UndirectedData;
                data.MakeConstraintEdge();
                entry.UndirectedData = data;
                _dcel.Edges[e.Index] = entry;
            }
        }
        
        MakeTemp(firstBorderEdge.AsUndirected());
        MakeTemp(lastBorderEdge.AsUndirected());
        
        var current = firstBorderEdge;
        FixedDirectedEdgeHandle? result = null;
        
        while (current != lastBorderEdge.Rev())
        {
            var handle = DirectedEdge(current);
            var next = handle.Next().Handle.AsUndirected();
            
            current = handle.CCW().Handle;
            
            if (targetVertex == handle.To().Handle)
            {
                MakeConstraintEdge(handle.Handle.AsUndirected());
                result = handle.Handle;
            }
            MakeTemp(next);
        }
        
        // Legalize
        foreach (var edge in conflictEdges)
        {
            LegalizeEdge(edge, false);
        }
        
        // Undo temp
        foreach (var e in tempConstraints)
        {
            var entry = _dcel.Edges[e.Index];
            var data = entry.UndirectedData;
            data.UnmakeConstraintEdge();
            entry.UndirectedData = data;
            _dcel.Edges[e.Index] = entry;
        }
        
        return result;
    }

    private bool MakeConstraintEdge(FixedUndirectedEdgeHandle edge)
    {
        if (!IsConstraint(edge))
        {
            var entry = _dcel.Edges[edge.Index];
            var data = entry.UndirectedData;
            data.MakeConstraintEdge();
            entry.UndirectedData = data;
            _dcel.Edges[edge.Index] = entry;
            _numConstraints++;
            return true;
        }
        return false;
    }
}
