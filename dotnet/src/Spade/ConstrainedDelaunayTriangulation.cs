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

    public bool AddConstraintWithSplitting(
        FixedVertexHandle from,
        FixedVertexHandle to,
        Func<Point2<double>, V> vertexConstructor)
    {
        if (vertexConstructor == null) throw new ArgumentNullException(nameof(vertexConstructor));

        var initialNumConstraints = NumConstraints;
        ResolveSplittingConstraintRequest(from, to, vertexConstructor);
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
                var edge = edgeInt.Edge;
                if (IsConstraint(edge.Handle.AsUndirected()))
                {
                    if (vertexConstructor == null)
                    {
                        throw new InvalidOperationException("Constraint intersection detected and no vertex constructor provided (splitting disabled)");
                    }

                    // Compute intersection point
                    var p0 = edge.From().Data.Position;
                    var p1 = edge.To().Data.Position;
                    var fromPos = Vertex(from).Data.Position;
                    var toPos = Vertex(to).Data.Position;

                    var lineIntersection = MathUtils.GetEdgeIntersections(p0, p1, fromPos, toPos);
                    lineIntersection = MathUtils.MitigateUnderflow(lineIntersection);

                    var newVertex = vertexConstructor(lineIntersection);
                    var position = newVertex.Position;

                    // Validate split position - check if it's close to an existing vertex
                    var alternativeVertex = ValidateSplitPosition(edge, position);

                    FixedVertexHandle finalVertex;
                    if (alternativeVertex.HasValue)
                    {
                        var (altVertex, isEndVertex) = alternativeVertex.Value;
                        if (!isEndVertex)
                        {
                            // Alternative vertex is an opposite vertex - adjust constraint edges
                            var isOnSameSide = edge.OppositeVertex()?.Handle == altVertex;
                            if (!isOnSameSide)
                            {
                                edge = edge.Rev();
                            }

                            var prev = edge.Prev().Handle;
                            var next = edge.Next().Handle;
                            var edgeHandle = edge.Handle;

                            // Unmake the original constraint edge
                            UnmakeConstraintEdge(edgeHandle.AsUndirected());

                            // Make the two new edges constraints
                            MakeConstraintEdge(prev.AsUndirected());
                            MakeConstraintEdge(next.AsUndirected());

                            // Legalize the edge
                            LegalizeEdge(edgeHandle, false);
                        }
                        finalVertex = altVertex;
                    }
                    else
                    {
                        // Insert new vertex on the edge
                        var edgeHandle = edge.Handle;
                        var newVertexHandle = DcelOperations.AppendUnconnectedVertex(_dcel, newVertex);
                        var splitEdges = InsertOnEdge(edgeHandle, newVertexHandle);
                        HandleLegalEdgeSplit(splitEdges);
                        LegalizeVertex(newVertexHandle);
                        finalVertex = newVertexHandle;
                    }

                    // Add constraint from current 'from' to the split vertex
                    var previousRegion = TryAddConstraint(from, finalVertex);
                    result.AddRange(previousRegion);
                    conflictEdges.Clear();

                    from = finalVertex;
                    // Reset iterator from the split vertex
                    iterator = LineIntersectionIterator<V, DE, CdtEdge<UE>, F, L>.NewFromHandles(this, from, to);
                    iterator.MoveNext(); // Skip start vertex
                }
                else
                {
                    conflictEdges.Add(edge.Handle);
                }
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

    private void UnmakeConstraintEdge(FixedUndirectedEdgeHandle edge)
    {
        if (IsConstraint(edge))
        {
            var entry = _dcel.Edges[edge.Index];
            var data = entry.UndirectedData;
            data.UnmakeConstraintEdge();
            entry.UndirectedData = data;
            _dcel.Edges[edge.Index] = entry;
            _numConstraints--;
        }
    }

    private List<FixedDirectedEdgeHandle> TryAddConstraint(FixedVertexHandle from, FixedVertexHandle to)
    {
        var result = new List<FixedDirectedEdgeHandle>();
        if (from == to) return result;

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
                    // Cannot add - intersects constraint
                    return new List<FixedDirectedEdgeHandle>();
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
                    iterator.MoveNext();
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

    private (FixedVertexHandle, bool)? ValidateSplitPosition(
        DirectedEdgeHandle<V, DE, CdtEdge<UE>, F> conflictEdge,
        Point2<double> splitPosition)
    {
        // Check if the split position is valid (on the edge or in neighboring faces)
        // If not, return an alternative vertex
        var location = LocateWithHintOptionCore(splitPosition, conflictEdge.From().Handle);

        bool isValid = location switch
        {
            PositionInTriangulation.OnEdge onEdge =>
                onEdge.Edge.AsUndirected() == conflictEdge.Handle.AsUndirected(),
            PositionInTriangulation.OnFace onFace =>
                onFace.Face == conflictEdge.Face().Handle || onFace.Face == conflictEdge.Rev().Face().Handle,
            PositionInTriangulation.OutsideOfConvexHull _ =>
                conflictEdge.Face().IsOuter || conflictEdge.Rev().Face().IsOuter,
            PositionInTriangulation.OnVertex _ => false,
            _ => false
        };

        if (isValid) return null;

        // Find closest alternative vertex
        var dFrom = conflictEdge.From().Data.Position.Sub(splitPosition).Length2();
        var dTo = conflictEdge.To().Data.Position.Sub(splitPosition).Length2();

        var minDistance = dFrom;
        var minVertex = conflictEdge.From().Handle;
        var isEndVertex = true;

        if (dTo < minDistance)
        {
            minDistance = dTo;
            minVertex = conflictEdge.To().Handle;
        }

        // Check opposite vertices
        var opposite = conflictEdge.OppositeVertex();
        if (opposite.HasValue)
        {
            var dOpposite = opposite.Value.Data.Position.Sub(splitPosition).Length2();
            if (dOpposite < minDistance)
            {
                minDistance = dOpposite;
                minVertex = opposite.Value.Handle;
                isEndVertex = false;
            }
        }

        var revOpposite = conflictEdge.Rev().OppositeVertex();
        if (revOpposite.HasValue)
        {
            var dRevOpposite = revOpposite.Value.Data.Position.Sub(splitPosition).Length2();
            if (dRevOpposite < minDistance)
            {
                minVertex = revOpposite.Value.Handle;
                isEndVertex = false;
            }
        }

        return (minVertex, isEndVertex);
    }

    private FixedDirectedEdgeHandle[] InsertOnEdge(FixedDirectedEdgeHandle edge, FixedVertexHandle newVertex)
    {
        return DcelOperations.SplitEdge(_dcel, edge, newVertex);
    }
}
