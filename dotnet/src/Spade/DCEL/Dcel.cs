using Spade.Handles;

namespace Spade.DCEL;

public class Dcel<V, DE, UE, F>
{
    internal List<VertexEntry<V>> Vertices { get; }
    internal List<FaceEntry<F>> Faces { get; }
    internal List<EdgeEntry<DE, UE>> Edges { get; }

    public Dcel() : this(0, 0, 0)
    {
    }

    public Dcel(int vertexCapacity, int edgeCapacity, int faceCapacity)
    {
        Vertices = new List<VertexEntry<V>>(vertexCapacity);
        Faces = new List<FaceEntry<F>>(Math.Max(1, faceCapacity)); // At least 1 for outer face
        Edges = new List<EdgeEntry<DE, UE>>(edgeCapacity);

        // Initialize with outer face
        // Rust implementation calls dcel_operations::new() which likely creates the outer face.
        // Based on dcel.rs: self.faces.truncate(1); // Keep outer face
        // So there is always at least one face (outer face).
        Faces.Add(new FaceEntry<F>
        {
            AdjacentEdge = null,
            Data = default!
        });
    }

    /// <summary>
    /// Ensures the vertices collection has at least the specified capacity.
    /// </summary>
    /// <param name="capacity">Minimum capacity required</param>
    public void EnsureVertexCapacity(int capacity)
    {
        if (Vertices.Capacity < capacity)
        {
            Vertices.Capacity = capacity;
        }
    }

    /// <summary>
    /// Ensures the edges collection has at least the specified capacity.
    /// </summary>
    /// <param name="capacity">Minimum capacity required</param>
    public void EnsureEdgeCapacity(int capacity)
    {
        if (Edges.Capacity < capacity)
        {
            Edges.Capacity = capacity;
        }
    }

    /// <summary>
    /// Ensures the faces collection has at least the specified capacity.
    /// </summary>
    /// <param name="capacity">Minimum capacity required</param>
    public void EnsureFaceCapacity(int capacity)
    {
        if (Faces.Capacity < capacity)
        {
            Faces.Capacity = capacity;
        }
    }

    public int NumVertices => Vertices.Count;
    public int NumDirectedEdges => Edges.Count * 2;
    public int NumUndirectedEdges => Edges.Count;
    public int NumFaces => Faces.Count;

    public VertexHandle<V, DE, UE, F> Vertex(FixedVertexHandle handle)
    {
        return new VertexHandle<V, DE, UE, F>(this, handle);
    }

    public DirectedEdgeHandle<V, DE, UE, F> DirectedEdge(FixedDirectedEdgeHandle handle)
    {
        return new DirectedEdgeHandle<V, DE, UE, F>(this, handle);
    }

    public UndirectedEdgeHandle<V, DE, UE, F> UndirectedEdge(FixedUndirectedEdgeHandle handle)
    {
        return new UndirectedEdgeHandle<V, DE, UE, F>(this, handle);
    }

    public FaceHandle<V, DE, UE, F> Face(FixedFaceHandle handle)
    {
        return new FaceHandle<V, DE, UE, F>(this, handle);
    }

    public FaceHandle<V, DE, UE, F> OuterFace()
    {
        return Face(new FixedFaceHandle(0));
    }

    public DirectedEdgeHandle<V, DE, UE, F>? GetEdgeFromNeighbors(FixedVertexHandle from, FixedVertexHandle to)
    {
        var vertex = Vertices[from.Index];
        if (!vertex.OutEdge.HasValue) return null;

        var startEdge = vertex.OutEdge.Value;
        var current = startEdge;
        var visitedEdges = new HashSet<int>();
        int maxIterations = 1000; // Safety limit
        int iterations = 0;

        do
        {
            // Safety checks to prevent infinite loops
            if (iterations++ >= maxIterations)
            {
                throw new InvalidOperationException(
                    $"Exceeded maximum iterations ({maxIterations}) while searching for edge from vertex {from.Index} to {to.Index}. " +
                    "This likely indicates a malformed DCEL structure.");
            }

            if (!visitedEdges.Add(current.Index))
            {
                throw new InvalidOperationException(
                    $"Detected cycle while searching for edge from vertex {from.Index} to {to.Index} at edge {current.Index}. " +
                    "This indicates a malformed DCEL structure.");
            }

            var edgeHandle = DirectedEdge(current);
            if (edgeHandle.To().Handle == to)
            {
                return edgeHandle;
            }
            current = edgeHandle.CCW().Handle;
        } while (current.Index != startEdge.Index);

        return null;
    }

    internal HalfEdgeEntry GetHalfEdge(FixedDirectedEdgeHandle handle)
    {
        return Edges[handle.Index / 2].Entries[handle.Index % 2];
    }

    internal void UpdateHalfEdge(FixedDirectedEdgeHandle handle, Action<HalfEdgeEntry> update)
    {
        // We need to update the struct in the array in the list
        var edgeIndex = handle.Index / 2;
        var entryIndex = handle.Index % 2;
        var edgeEntry = Edges[edgeIndex];

        // Create a copy, update it
        var halfEdge = edgeEntry.Entries[entryIndex];
        // We can't pass a struct to Action and expect it to modify the original if it's by value.
        // But here we want to modify the copy and then put it back.
        // Actually, Action<HalfEdgeEntry> would receive a copy.
        // We need a ref or just return the modified one.
        // Let's change the signature to take a modifier function that returns the new state
        // OR just expose a way to set it.
    }

    internal void UpdateHalfEdge(FixedDirectedEdgeHandle handle, Func<HalfEdgeEntry, HalfEdgeEntry> update)
    {
        var edgeIndex = handle.Index / 2;
        var entryIndex = handle.Index % 2;
        var edgeEntry = Edges[edgeIndex];
        edgeEntry.Entries[entryIndex] = update(edgeEntry.Entries[entryIndex]);
        Edges[edgeIndex] = edgeEntry;
    }

    // Overload for convenience when we want to modify properties
    internal void UpdateHalfEdge(FixedDirectedEdgeHandle handle, Action<Ref<HalfEdgeEntry>> update)
    {
        var edgeIndex = handle.Index / 2;
        var entryIndex = handle.Index % 2;
        var edgeEntry = Edges[edgeIndex];

        var wrapper = new Ref<HalfEdgeEntry>(edgeEntry.Entries[entryIndex]);
        update(wrapper);
        edgeEntry.Entries[entryIndex] = wrapper.Value;

        Edges[edgeIndex] = edgeEntry;
    }
}

internal class Ref<T> where T : struct
{
    public T Value;
    public Ref(T value) { Value = value; }
}
