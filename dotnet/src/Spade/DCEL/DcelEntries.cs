using Spade.Handles;

namespace Spade.DCEL;

internal struct FaceEntry<F>
{
    public FixedDirectedEdgeHandle? AdjacentEdge;
    public F Data;
}

internal struct VertexEntry<V>
{
    public V Data;
    public FixedDirectedEdgeHandle? OutEdge;
}

internal struct HalfEdgeEntry
{
    public FixedDirectedEdgeHandle Next;
    public FixedDirectedEdgeHandle Prev;
    public FixedFaceHandle Face;
    public FixedVertexHandle Origin;
}

internal struct EdgeEntry<DE, UE>
{
    public HalfEdgeEntry[] Entries; // Size 2
    public DE[] DirectedData;       // Size 2
    public UE UndirectedData;

    public EdgeEntry(HalfEdgeEntry normalized, HalfEdgeEntry notNormalized)
    {
        Entries = new HalfEdgeEntry[] { normalized, notNormalized };
        DirectedData = new DE[2];
        UndirectedData = default!;
    }
}
