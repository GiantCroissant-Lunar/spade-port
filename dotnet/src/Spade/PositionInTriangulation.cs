using Spade.Handles;

namespace Spade;

public abstract record PositionInTriangulation
{
    public sealed record OnVertex(FixedVertexHandle Vertex) : PositionInTriangulation;
    public sealed record OnEdge(FixedDirectedEdgeHandle Edge) : PositionInTriangulation;
    public sealed record OnFace(FixedFaceHandle Face) : PositionInTriangulation;
    public sealed record OutsideOfConvexHull(FixedDirectedEdgeHandle Edge) : PositionInTriangulation;
    public sealed record NoTriangulation : PositionInTriangulation;
}

internal enum PositionWhenAllVerticesOnLineType
{
    OnEdge,
    OnVertex,
    NotOnLine,
    ExtendingLine
}

internal readonly struct PositionWhenAllVerticesOnLine
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
