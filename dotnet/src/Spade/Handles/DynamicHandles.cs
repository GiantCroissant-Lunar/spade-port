using Spade.DCEL;
using Spade.Primitives;

namespace Spade.Handles;

public readonly struct VertexHandle<V, DE, UE, F>
{
    private readonly Dcel<V, DE, UE, F> _dcel;
    public FixedVertexHandle Handle { get; }

    public VertexHandle(Dcel<V, DE, UE, F> dcel, FixedVertexHandle handle)
    {
        _dcel = dcel;
        Handle = handle;
    }

    public V Data => _dcel.Vertices[Handle.Index].Data;

    public DirectedEdgeHandle<V, DE, UE, F>? OutEdge()
    {
        var edge = _dcel.Vertices[Handle.Index].OutEdge;
        return edge.HasValue ? new DirectedEdgeHandle<V, DE, UE, F>(_dcel, edge.Value) : null;
    }
}

public readonly struct DirectedEdgeHandle<V, DE, UE, F>
{
    private readonly Dcel<V, DE, UE, F> _dcel;
    public FixedDirectedEdgeHandle Handle { get; }

    public DirectedEdgeHandle(Dcel<V, DE, UE, F> dcel, FixedDirectedEdgeHandle handle)
    {
        _dcel = dcel;
        Handle = handle;
    }

    public DE Data => _dcel.Edges[Handle.Index / 2].DirectedData[Handle.Index % 2];

    public VertexHandle<V, DE, UE, F> From()
    {
        var originHandle = _dcel.Edges[Handle.Index / 2].Entries[Handle.Index % 2].Origin;
        return new VertexHandle<V, DE, UE, F>(_dcel, originHandle);
    }

    public VertexHandle<V, DE, UE, F> To()
    {
        return Rev().From();
    }

    public DirectedEdgeHandle<V, DE, UE, F> Rev()
    {
        return new DirectedEdgeHandle<V, DE, UE, F>(_dcel, Handle.Rev());
    }

    public DirectedEdgeHandle<V, DE, UE, F> Next()
    {
        var nextHandle = _dcel.Edges[Handle.Index / 2].Entries[Handle.Index % 2].Next;
        return new DirectedEdgeHandle<V, DE, UE, F>(_dcel, nextHandle);
    }

    public DirectedEdgeHandle<V, DE, UE, F> Prev()
    {
        var prevHandle = _dcel.Edges[Handle.Index / 2].Entries[Handle.Index % 2].Prev;
        return new DirectedEdgeHandle<V, DE, UE, F>(_dcel, prevHandle);
    }

    public FaceHandle<V, DE, UE, F> Face()
    {
        var faceHandle = _dcel.Edges[Handle.Index / 2].Entries[Handle.Index % 2].Face;
        return new FaceHandle<V, DE, UE, F>(_dcel, faceHandle);
    }

    public bool IsOuterEdge()
    {
        return Face().IsOuter;
    }

    public DirectedEdgeHandle<V, DE, UE, F> CCW()
    {
        return Prev().Rev();
    }

    public DirectedEdgeHandle<V, DE, UE, F> CW()
    {
        return Rev().Next();
    }

    public VertexHandle<V, DE, UE, F>? OppositeVertex()
    {
        if (Face().IsOuter)
        {
            return null;
        }
        return Next().To();
    }

    public LineSideInfo SideQuery(Point2<double> position)
    {
        var p1 = ((IHasPosition<double>)From().Data).Position;
        var p2 = ((IHasPosition<double>)To().Data).Position;
        return MathUtils.SideQuery(p1, p2, position);
    }

    public UndirectedEdgeHandle<V, DE, UE, F> AsUndirected()
    {
        return new UndirectedEdgeHandle<V, DE, UE, F>(_dcel, Handle.AsUndirected());
    }
}

public readonly struct UndirectedEdgeHandle<V, DE, UE, F>
{
    private readonly Dcel<V, DE, UE, F> _dcel;
    public FixedUndirectedEdgeHandle Handle { get; }

    public UndirectedEdgeHandle(Dcel<V, DE, UE, F> dcel, FixedUndirectedEdgeHandle handle)
    {
        _dcel = dcel;
        Handle = handle;
    }

    public UE Data => _dcel.Edges[Handle.Index].UndirectedData;
}

public readonly struct FaceHandle<V, DE, UE, F>
{
    private readonly Dcel<V, DE, UE, F> _dcel;
    public FixedFaceHandle Handle { get; }

    public FaceHandle(Dcel<V, DE, UE, F> dcel, FixedFaceHandle handle)
    {
        _dcel = dcel;
        Handle = handle;
    }

    public F Data => _dcel.Faces[Handle.Index].Data;

    public bool IsOuter => Handle.Index == 0; // Assuming 0 is always outer face

    public DirectedEdgeHandle<V, DE, UE, F>? AdjacentEdge()
    {
        var edge = _dcel.Faces[Handle.Index].AdjacentEdge;
        return edge.HasValue ? new DirectedEdgeHandle<V, DE, UE, F>(_dcel, edge.Value) : null;
    }

    public Point2<double> Circumcenter()
    {
        var edge = AdjacentEdge() ?? throw new InvalidOperationException("Face has no adjacent edge");
        var v0 = ((IHasPosition<double>)edge.From().Data).Position;
        var v1 = ((IHasPosition<double>)edge.To().Data).Position;
        var v2 = ((IHasPosition<double>)edge.Next().To().Data).Position;

        return MathUtils.Circumcenter(v0, v1, v2);
    }
}
