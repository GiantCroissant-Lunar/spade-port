using System.Numerics;
using Spade.DCEL;
using Spade.Handles;
using Spade.Primitives;
using Spade.Voronoi;

namespace Spade;

public abstract class TriangulationBase<V, DE, UE, F, L>
    where V : IHasPosition<double>, new()
    where DE : new()
    where UE : new()
    where F : new()
    where L : IHintGenerator<double>, new()
{
    internal Dcel<V, DE, UE, F> _dcel;
    internal L _hintGenerator;

    public TriangulationBase()
    {
        _dcel = new Dcel<V, DE, UE, F>();
        _hintGenerator = new L();
    }

    public int NumVertices => _dcel.NumVertices;
    public int NumFaces => _dcel.NumFaces;
    public int NumUndirectedEdges => _dcel.NumUndirectedEdges;
    public int NumDirectedEdges => _dcel.NumDirectedEdges;

    public VertexHandle<V, DE, UE, F> Vertex(FixedVertexHandle handle) => _dcel.Vertex(handle);
    public DirectedEdgeHandle<V, DE, UE, F> DirectedEdge(FixedDirectedEdgeHandle handle) => _dcel.DirectedEdge(handle);
    public UndirectedEdgeHandle<V, DE, UE, F> UndirectedEdge(FixedUndirectedEdgeHandle handle) => _dcel.UndirectedEdge(handle);
    public FaceHandle<V, DE, UE, F> Face(FixedFaceHandle handle) => _dcel.Face(handle);
    public FaceHandle<V, DE, UE, F> OuterFace() => _dcel.OuterFace();

    public IEnumerable<VertexHandle<V, DE, UE, F>> Vertices()
    {
        for (int i = 0; i < NumVertices; i++)
        {
            yield return Vertex(new FixedVertexHandle(i));
        }
    }

    public IEnumerable<DirectedEdgeHandle<V, DE, UE, F>> DirectedEdges()
    {
        for (int i = 0; i < NumDirectedEdges; i++)
        {
            yield return DirectedEdge(new FixedDirectedEdgeHandle(i));
        }
    }

    public IEnumerable<UndirectedEdgeHandle<V, DE, UE, F>> UndirectedEdges()
    {
        for (int i = 0; i < NumUndirectedEdges; i++)
        {
            yield return UndirectedEdge(new FixedUndirectedEdgeHandle(i));
        }
    }

    public IEnumerable<VoronoiFace<V, DE, UE, F>> VoronoiFaces()
    {
        foreach (var vertex in Vertices())
        {
            yield return new VoronoiFace<V, DE, UE, F>(vertex);
        }
    }

    public IEnumerable<FaceHandle<V, DE, UE, F>> InnerFaces()
    {
        for (int i = 1; i < NumFaces; i++) // Skip outer face (index 0)
        {
            yield return Face(new FixedFaceHandle(i));
        }
    }

    public IEnumerable<DirectedVoronoiEdge<V, DE, UE, F>> DirectedVoronoiEdges()
    {
        foreach (var edge in DirectedEdges())
        {
            yield return new DirectedVoronoiEdge<V, DE, UE, F>(edge);
        }
    }

    public IEnumerable<UndirectedVoronoiEdge<V, DE, UE, F>> UndirectedVoronoiEdges()
    {
        foreach (var edge in UndirectedEdges())
        {
            yield return new UndirectedVoronoiEdge<V, DE, UE, F>(edge);
        }
    }

    public FixedVertexHandle Insert(V vertex)
    {
        return InsertWithHintOption(vertex, null);
    }

    public FixedVertexHandle InsertWithHintOption(V vertex, FixedVertexHandle? hint)
    {
        MathUtils.ValidateCoordinate(vertex.Position.X);
        MathUtils.ValidateCoordinate(vertex.Position.Y);

        var position = vertex.Position;
        var result = InsertWithHintOptionImpl(new VertexToInsert.NewVertex(vertex), hint);

        _hintGenerator.NotifyVertexInserted(result, position);
        return result;
    }

    private FixedVertexHandle InsertWithHintOptionImpl(VertexToInsert t, FixedVertexHandle? hint)
    {
        if (NumVertices == 0)
        {
            return t.IntoVertex(_dcel);
        }

        var pos = t.GetPosition(_dcel);

        if (NumVertices == 1)
        {
            var v0 = new FixedVertexHandle(0);
            if (Vertex(v0).Data.Position == pos)
            {
                return t.Update(v0, _dcel);
            }
            
            var v1 = t.IntoVertex(_dcel);
            DcelOperations.SetupInitialTwoVertices(_dcel, v0, v1);
            return v1;
        }

        if (AllVerticesOnLine())
        {
            var location = LocateWhenAllVerticesOnLine(pos);
            return InsertWhenAllVerticesOnLine(location, t);
        }
        else
        {
            var positionInTriangulation = LocateWithHintOptionCore(pos, hint);
            return positionInTriangulation switch
            {
                PositionInTriangulation.OutsideOfConvexHull outside => InsertOutsideOfConvexHull(outside.Edge, t.Resolve(_dcel)),
                PositionInTriangulation.OnFace onFace => InsertIntoFace(onFace.Face, t.Resolve(_dcel)),
                PositionInTriangulation.OnEdge onEdge => InsertOnEdge(onEdge.Edge, t.Resolve(_dcel)),
                PositionInTriangulation.OnVertex onVertex => t.Update(onVertex.Vertex, _dcel),
                _ => throw new InvalidOperationException("Error during vertex lookup. This is a bug.")
            };
        }
    }

    private FixedVertexHandle UpdateVertex(FixedVertexHandle handle, V vertex)
    {
        var v = _dcel.Vertices[handle.Index];
        v.Data = vertex;
        _dcel.Vertices[handle.Index] = v;
        return handle;
    }

    private FixedVertexHandle InsertOnEdge(FixedDirectedEdgeHandle edge, FixedVertexHandle newVertex)
    {
        var edgeHandle = DirectedEdge(edge);
        FixedDirectedEdgeHandle[] splitParts;
        
        if (edgeHandle.Face().IsOuter)
        {
            var parts = DcelOperations.SplitHalfEdge(_dcel, edge.Rev(), newVertex);
            splitParts = new[] { parts[1].Rev(), parts[0].Rev() };
        }
        else if (edgeHandle.Rev().Face().IsOuter)
        {
            splitParts = DcelOperations.SplitHalfEdge(_dcel, edge, newVertex);
        }
        else
        {
            splitParts = DcelOperations.SplitEdge(_dcel, edge, newVertex);
        }

        HandleLegalEdgeSplit(splitParts);
        LegalizeVertex(newVertex);
        return newVertex;
    }

    protected virtual void HandleLegalEdgeSplit(FixedDirectedEdgeHandle[] handles)
    {
        // Default implementation does nothing
    }

    private FixedVertexHandle InsertIntoFace(FixedFaceHandle face, FixedVertexHandle t)
    {
        var newHandle = DcelOperations.InsertIntoTriangle(_dcel, t, face);
        LegalizeVertex(newHandle);
        return newHandle;
    }

    private FixedVertexHandle InsertOutsideOfConvexHull(FixedDirectedEdgeHandle convexHullEdge, FixedVertexHandle newVertex)
    {
        var position = Vertex(newVertex).Data.Position;
        
        if (!DirectedEdge(convexHullEdge).SideQuery(position).IsOnLeftSide)
        {
             throw new InvalidOperationException("Point must be on left side of convex hull edge");
        }

        var result = DcelOperations.CreateNewFaceAdjacentToEdge(_dcel, convexHullEdge, newVertex);

        var ccwWalkStart = DirectedEdge(convexHullEdge).Prev().Rev().Handle;
        var cwWalkStart = DirectedEdge(convexHullEdge).Next().Rev().Handle;

        LegalizeEdge(convexHullEdge, false);

        var currentEdge = ccwWalkStart;
        while (true)
        {
            var handle = DirectedEdge(currentEdge);
            var prev = handle.Prev();
            currentEdge = prev.Handle;
            if (prev.SideQuery(position).IsOnLeftSide)
            {
                var newEdge = DcelOperations.CreateSingleFaceBetweenEdgeAndNext(_dcel, currentEdge);
                LegalizeEdge(currentEdge, false);
                currentEdge = newEdge;
            }
            else
            {
                break;
            }
        }

        currentEdge = cwWalkStart;
        while (true)
        {
            var handle = DirectedEdge(currentEdge);
            var next = handle.Next();
            var nextFix = next.Handle;
            if (next.SideQuery(position).IsOnLeftSide)
            {
                var newEdge = DcelOperations.CreateSingleFaceBetweenEdgeAndNext(_dcel, currentEdge);
                LegalizeEdge(nextFix, false);
                currentEdge = newEdge;
            }
            else
            {
                break;
            }
        }

        return result;
    }

    protected void LegalizeVertex(FixedVertexHandle newHandle)
    {
        var edges = new List<FixedDirectedEdgeHandle>();
        var vertex = Vertex(newHandle);
        
        var startEdge = _dcel.Vertices[newHandle.Index].OutEdge;
        if (startEdge.HasValue)
        {
            var current = startEdge.Value;
            do
            {
                var edge = DirectedEdge(current);
                if (!edge.Face().IsOuter)
                {
                    edges.Add(edge.Next().Handle);
                }
                current = edge.CCW().Handle;
            } while (current != startEdge.Value);
        }

        foreach (var edgeToLegalize in edges)
        {
            LegalizeEdge(edgeToLegalize, false);
        }
    }

    protected virtual bool IsConstraint(FixedUndirectedEdgeHandle edge)
    {
        return false;
    }

    protected bool LegalizeEdge(FixedDirectedEdgeHandle edge, bool fullyLegalize)
    {
        var edges = new Stack<FixedDirectedEdgeHandle>();
        edges.Push(edge);
        var result = false;

        while (edges.Count > 0)
        {
            var e = edges.Pop();
            
            if (IsConstraint(e.AsUndirected())) continue;

            var edgeHandle = DirectedEdge(e);
            var revHandle = edgeHandle.Rev();
            
            if (edgeHandle.Face().IsOuter || revHandle.Face().IsOuter)
            {
                continue;
            }

            var v0 = edgeHandle.From().Data.Position;
            var v1 = edgeHandle.To().Data.Position;
            var v2 = edgeHandle.Next().To().Data.Position;
            var v3 = revHandle.Next().To().Data.Position;

            if (MathUtils.ContainedInCircumference(v2, v1, v0, v3))
            {
                result = true;
                
                edges.Push(revHandle.Next().Handle);
                edges.Push(revHandle.Prev().Handle);
                
                if (fullyLegalize)
                {
                    edges.Push(edgeHandle.Next().Handle);
                    edges.Push(edgeHandle.Prev().Handle);
                }
                
                DcelOperations.FlipCw(_dcel, e.AsUndirected());
            }
        }
        return result;
    }

    public DirectedEdgeHandle<V, DE, UE, F>? GetEdgeFromNeighbors(
        FixedVertexHandle from,
        FixedVertexHandle to)
    {
        return _dcel.GetEdgeFromNeighbors(from, to);
    }

    public PositionInTriangulation LocateWithHintOptionCore(Point2<double> point, FixedVertexHandle? hint)
    {
        var start = hint ?? _hintGenerator.GetHint(point);
        return LocateWithHintFixedCore(point, start);
    }

    private PositionInTriangulation LocateWithHintFixedCore(Point2<double> targetPosition, FixedVertexHandle start)
    {
        if (NumVertices < 2)
        {
            if (NumVertices == 1 && Vertex(new FixedVertexHandle(0)).Data.Position == targetPosition)
            {
                return new PositionInTriangulation.OnVertex(new FixedVertexHandle(0));
            }
            return new PositionInTriangulation.NoTriangulation();
        }

        if (AllVerticesOnLine())
        {
             throw new NotImplementedException("LocateWhenAllVerticesOnLine not implemented yet");
        }

        start = ValidateVertexHandle(start);
        var closestVertex = WalkToNearestNeighbor(start, targetPosition);

        var e0 = _dcel.Vertices[closestVertex.Handle.Index].OutEdge ?? throw new InvalidOperationException("No out edge found");
        var e0Handle = DirectedEdge(e0);
        var e0Query = e0Handle.SideQuery(targetPosition);
        var rotateCcw = e0Query.IsOnLeftSideOrOnLine;

        var loopCounter = NumDirectedEdges;
        while (true)
        {
            if (loopCounter-- == 0) throw new InvalidOperationException("Infinite loop in Locate");

            var from = e0Handle.From();
            var to = e0Handle.To();

            if (from.Data.Position == targetPosition)
            {
                _hintGenerator.NotifyVertexLookup(from.Handle);
                return new PositionInTriangulation.OnVertex(from.Handle);
            }
            if (to.Data.Position == targetPosition)
            {
                _hintGenerator.NotifyVertexLookup(to.Handle);
                return new PositionInTriangulation.OnVertex(to.Handle);
            }

            if (e0Query.IsOnLine)
            {
                if (e0Handle.Face().IsOuter)
                {
                    e0Handle = e0Handle.Rev();
                }
                e0Handle = e0Handle.Prev();
                e0Query = e0Handle.SideQuery(targetPosition);
                rotateCcw = e0Query.IsOnLeftSideOrOnLine;
                continue;
            }

            var e1Handle = rotateCcw ? e0Handle : e0Handle.Rev();
            
            if (e1Handle.Face().IsOuter)
            {
                _hintGenerator.NotifyVertexLookup(e1Handle.From().Handle);
                return new PositionInTriangulation.OutsideOfConvexHull(e1Handle.Handle);
            }

            var rotated = rotateCcw ? e0Handle.CCW() : e0Handle.CW();

            var rotatedQuery = rotated.SideQuery(targetPosition);
            if (rotatedQuery.IsOnLine || rotatedQuery.IsOnLeftSide == rotateCcw)
            {
                e0Handle = rotated;
                e0Query = rotatedQuery;
                continue;
            }

            var e2Handle = rotateCcw ? e1Handle.Next() : e1Handle.Prev();
            var e2Query = e2Handle.SideQuery(targetPosition);

            if (e2Query.IsOnLine)
            {
                _hintGenerator.NotifyVertexLookup(e2Handle.To().Handle);
                return new PositionInTriangulation.OnEdge(e2Handle.Handle);
            }
            if (e2Query.IsOnLeftSide)
            {
                _hintGenerator.NotifyVertexLookup(e2Handle.To().Handle);
                return new PositionInTriangulation.OnFace(e1Handle.Face().Handle);
            }

            e0Handle = e2Handle.Rev();
            e0Query = e2Query.Reversed();
            if (!e0Handle.Face().IsOuter)
            {
                e0Handle = e0Handle.Prev();
                e0Query = e0Handle.SideQuery(targetPosition);
            }
            rotateCcw = e0Query.IsOnLeftSideOrOnLine;
        }
    }

    private VertexHandle<V, DE, UE, F> WalkToNearestNeighbor(FixedVertexHandle start, Point2<double> position)
    {
        var currentVertex = Vertex(start);
        var currentPos = currentVertex.Data.Position;
        
        if (currentPos == position) return currentVertex;

        var currentMinDist = (position.Sub(currentPos)).Length2();

        while (true)
        {
            var nextCandidate = currentVertex;
            var foundBetter = false;

            var startEdge = _dcel.Vertices[currentVertex.Handle.Index].OutEdge;
            if (startEdge.HasValue)
            {
                var currentEdge = startEdge.Value;
                do
                {
                    var edge = DirectedEdge(currentEdge);
                    var neighbor = edge.To();
                    var neighborPos = neighbor.Data.Position;
                    var dist = (position.Sub(neighborPos)).Length2();
                    
                    if (dist < currentMinDist)
                    {
                        currentMinDist = dist;
                        nextCandidate = neighbor;
                        foundBetter = true;
                    }

                    currentEdge = edge.CCW().Handle;
                } while (currentEdge != startEdge.Value);
            }

            if (!foundBetter) break;
            currentVertex = nextCandidate;
        }
        return currentVertex;
    }

    private FixedVertexHandle ValidateVertexHandle(FixedVertexHandle handle)
    {
        return handle.Index < NumVertices ? handle : new FixedVertexHandle(0);
    }

    private bool AllVerticesOnLine()
    {
        return NumFaces == 1;
    }

    private FixedVertexHandle InsertSecondVertex(V vertex)
    {
        var firstVertex = new FixedVertexHandle(0);
        if (Vertex(firstVertex).Data.Position == vertex.Position)
        {
            return UpdateVertex(firstVertex, vertex);
        }

        var secondVertex = DcelOperations.AppendUnconnectedVertex(_dcel, vertex);
        DcelOperations.SetupInitialTwoVertices(_dcel, firstVertex, secondVertex);
        return secondVertex;
    }

    private PositionWhenAllVerticesOnLine LocateWhenAllVerticesOnLine(Point2<double> position)
    {
        var edge = DirectedEdge(new FixedDirectedEdgeHandle(0));
        var query = edge.SideQuery(position);
        
        if (query.IsOnLeftSide) return PositionWhenAllVerticesOnLine.NotOnLine(edge.Handle);
        if (query.IsOnRightSide) return PositionWhenAllVerticesOnLine.NotOnLine(edge.Handle.Rev());

        var vertices = new List<VertexHandle<V, DE, UE, F>>();
        for(int i=0; i<NumVertices; i++) vertices.Add(Vertex(new FixedVertexHandle(i)));
        
        vertices.Sort((a, b) => 
        {
            var pa = a.Data.Position;
            var pb = b.Data.Position;
            if (pa.X != pb.X) return pa.X.CompareTo(pb.X);
            return pa.Y.CompareTo(pb.Y);
        });

        for (int i = 0; i < vertices.Count; i++)
        {
            var v = vertices[i];
            var vPos = v.Data.Position;
            
            if (vPos == position)
            {
                return PositionWhenAllVerticesOnLine.OnVertex(v.Handle);
            }
            
            bool isBefore;
            if (position.X != vPos.X) isBefore = position.X < vPos.X;
            else isBefore = position.Y < vPos.Y;
            
            if (isBefore)
            {
                if (i == 0)
                {
                    return PositionWhenAllVerticesOnLine.ExtendingLine(v.Handle);
                }
                else
                {
                    var prev = vertices[i-1];
                    var edgeHandle = GetEdgeFromNeighbors(prev.Handle, v.Handle);
                    if (edgeHandle == null) throw new InvalidOperationException("Collinear vertices not connected");
                    return PositionWhenAllVerticesOnLine.OnEdge(edgeHandle.Value.Handle);
                }
            }
        }
        
        return PositionWhenAllVerticesOnLine.ExtendingLine(vertices[vertices.Count - 1].Handle);
    }

    private FixedVertexHandle InsertWhenAllVerticesOnLine(PositionWhenAllVerticesOnLine location, VertexToInsert t)
    {
        switch (location.Type)
        {
            case PositionWhenAllVerticesOnLineType.OnEdge:
                var newVertex = t.Resolve(_dcel);
                DcelOperations.SplitEdgeWhenAllVerticesOnLine(_dcel, location.Edge!.Value, newVertex);
                return newVertex;
            case PositionWhenAllVerticesOnLineType.OnVertex:
                return t.Update(location.Vertex!.Value, _dcel);
            case PositionWhenAllVerticesOnLineType.NotOnLine:
                var nv = t.Resolve(_dcel);
                return InsertOutsideOfConvexHull(location.Edge!.Value, nv);
            case PositionWhenAllVerticesOnLineType.ExtendingLine:
                var nv2 = t.Resolve(_dcel);
                return DcelOperations.ExtendLine(_dcel, location.Vertex!.Value, nv2);
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private abstract record VertexToInsert
    {
        public abstract FixedVertexHandle IntoVertex(Dcel<V, DE, UE, F> dcel);
        public abstract Point2<double> GetPosition(Dcel<V, DE, UE, F> dcel);
        public abstract FixedVertexHandle Resolve(Dcel<V, DE, UE, F> dcel);
        public abstract FixedVertexHandle Update(FixedVertexHandle handle, Dcel<V, DE, UE, F> dcel);

        public sealed record NewVertex(V Vertex) : VertexToInsert
        {
            public override FixedVertexHandle IntoVertex(Dcel<V, DE, UE, F> dcel)
            {
                return DcelOperations.AppendUnconnectedVertex(dcel, Vertex);
            }

            public override Point2<double> GetPosition(Dcel<V, DE, UE, F> dcel)
            {
                return Vertex.Position;
            }

            public override FixedVertexHandle Resolve(Dcel<V, DE, UE, F> dcel)
            {
                return DcelOperations.AppendUnconnectedVertex(dcel, Vertex);
            }

            public override FixedVertexHandle Update(FixedVertexHandle handle, Dcel<V, DE, UE, F> dcel)
            {
                var v = dcel.Vertices[handle.Index];
                v.Data = Vertex;
                dcel.Vertices[handle.Index] = v;
                return handle;
            }
        }
        
        public sealed record ExistingVertex(FixedVertexHandle Handle) : VertexToInsert
        {
            public override FixedVertexHandle IntoVertex(Dcel<V, DE, UE, F> dcel)
            {
                return Handle;
            }

            public override Point2<double> GetPosition(Dcel<V, DE, UE, F> dcel)
            {
                return dcel.Vertices[Handle.Index].Data.Position;
            }

            public override FixedVertexHandle Resolve(Dcel<V, DE, UE, F> dcel)
            {
                return Handle;
            }

            public override FixedVertexHandle Update(FixedVertexHandle handle, Dcel<V, DE, UE, F> dcel)
            {
                if (Handle != handle)
                {
                    var v = dcel.Vertices[handle.Index];
                    v.Data = dcel.Vertices[Handle.Index].Data;
                    dcel.Vertices[handle.Index] = v;
                }
                return handle;
            }
        }
    }

    private enum PositionWhenAllVerticesOnLineType
    {
        OnEdge,
        OnVertex,
        NotOnLine,
        ExtendingLine
    }

    private readonly struct PositionWhenAllVerticesOnLine
    {
        public PositionWhenAllVerticesOnLineType Type { get; }
        public FixedDirectedEdgeHandle? Edge { get; }
        public FixedVertexHandle? Vertex { get; }

        private PositionWhenAllVerticesOnLine(PositionWhenAllVerticesOnLineType type, FixedDirectedEdgeHandle? edge, FixedVertexHandle? vertex)
        {
            Type = type;
            Edge = edge;
            Vertex = vertex;
        }

        public static PositionWhenAllVerticesOnLine OnEdge(FixedDirectedEdgeHandle edge) => new(PositionWhenAllVerticesOnLineType.OnEdge, edge, null);
        public static PositionWhenAllVerticesOnLine OnVertex(FixedVertexHandle vertex) => new(PositionWhenAllVerticesOnLineType.OnVertex, null, vertex);
        public static PositionWhenAllVerticesOnLine NotOnLine(FixedDirectedEdgeHandle edge) => new(PositionWhenAllVerticesOnLineType.NotOnLine, edge, null);
        public static PositionWhenAllVerticesOnLine ExtendingLine(FixedVertexHandle vertex) => new(PositionWhenAllVerticesOnLineType.ExtendingLine, null, vertex);
    }
}
