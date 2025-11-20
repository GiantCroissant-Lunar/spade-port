namespace Spade.Handles;

public readonly struct FixedVertexHandle : IEquatable<FixedVertexHandle>
{
    public int Index { get; }

    public FixedVertexHandle(int index)
    {
        Index = index;
    }

    public static implicit operator int(FixedVertexHandle handle) => handle.Index;
    public static explicit operator FixedVertexHandle(int index) => new FixedVertexHandle(index);

    public override string ToString() => $"Vertex({Index})";
    
    public bool Equals(FixedVertexHandle other) => Index == other.Index;
    public override bool Equals(object? obj) => obj is FixedVertexHandle other && Equals(other);
    public override int GetHashCode() => Index.GetHashCode();
    public static bool operator ==(FixedVertexHandle left, FixedVertexHandle right) => left.Equals(right);
    public static bool operator !=(FixedVertexHandle left, FixedVertexHandle right) => !left.Equals(right);
}

public readonly struct FixedDirectedEdgeHandle : IEquatable<FixedDirectedEdgeHandle>
{
    public int Index { get; }

    public FixedDirectedEdgeHandle(int index)
    {
        Index = index;
    }

    public FixedUndirectedEdgeHandle AsUndirected() => new FixedUndirectedEdgeHandle(Index / 2);
    public int NormalizeIndex() => Index % 2;
    public FixedDirectedEdgeHandle Rev() => new FixedDirectedEdgeHandle(Index ^ 1);

    public override string ToString() => $"DirectedEdge({Index})";

    public bool Equals(FixedDirectedEdgeHandle other) => Index == other.Index;
    public override bool Equals(object? obj) => obj is FixedDirectedEdgeHandle other && Equals(other);
    public override int GetHashCode() => Index.GetHashCode();
    public static bool operator ==(FixedDirectedEdgeHandle left, FixedDirectedEdgeHandle right) => left.Equals(right);
    public static bool operator !=(FixedDirectedEdgeHandle left, FixedDirectedEdgeHandle right) => !left.Equals(right);
}

public readonly struct FixedUndirectedEdgeHandle : IEquatable<FixedUndirectedEdgeHandle>
{
    public int Index { get; }

    public FixedUndirectedEdgeHandle(int index)
    {
        Index = index;
    }

    public override string ToString() => $"UndirectedEdge({Index})";

    public bool Equals(FixedUndirectedEdgeHandle other) => Index == other.Index;
    public override bool Equals(object? obj) => obj is FixedUndirectedEdgeHandle other && Equals(other);
    public override int GetHashCode() => Index.GetHashCode();
    public static bool operator ==(FixedUndirectedEdgeHandle left, FixedUndirectedEdgeHandle right) => left.Equals(right);
    public static bool operator !=(FixedUndirectedEdgeHandle left, FixedUndirectedEdgeHandle right) => !left.Equals(right);
}

public readonly struct FixedFaceHandle : IEquatable<FixedFaceHandle>
{
    public int Index { get; }

    public FixedFaceHandle(int index)
    {
        Index = index;
    }

    public override string ToString() => $"Face({Index})";

    public bool Equals(FixedFaceHandle other) => Index == other.Index;
    public override bool Equals(object? obj) => obj is FixedFaceHandle other && Equals(other);
    public override int GetHashCode() => Index.GetHashCode();
    public static bool operator ==(FixedFaceHandle left, FixedFaceHandle right) => left.Equals(right);
    public static bool operator !=(FixedFaceHandle left, FixedFaceHandle right) => !left.Equals(right);
}
