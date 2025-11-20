using Spade.Handles;
using Spade.Primitives;
using System.Numerics;

namespace Spade;

public interface IHintGenerator<S> where S : struct, INumber<S>, ISignedNumber<S>
{
    FixedVertexHandle GetHint(Point2<S> position);
    void NotifyVertexLookup(FixedVertexHandle handle);
    void NotifyVertexInserted(FixedVertexHandle handle, Point2<S> position);
    void NotifyVertexRemoved(Point2<S>? swappedInPoint, FixedVertexHandle removedHandle, Point2<S> removedPosition);
}

public class LastUsedVertexHintGenerator<S> : IHintGenerator<S> where S : struct, INumber<S>, ISignedNumber<S>
{
    private FixedVertexHandle _lastUsedVertex = new FixedVertexHandle(0);

    public FixedVertexHandle GetHint(Point2<S> position)
    {
        return _lastUsedVertex;
    }

    public void NotifyVertexLookup(FixedVertexHandle handle)
    {
        _lastUsedVertex = handle;
    }

    public void NotifyVertexInserted(FixedVertexHandle handle, Point2<S> position)
    {
        _lastUsedVertex = handle;
    }

    public void NotifyVertexRemoved(Point2<S>? swappedInPoint, FixedVertexHandle removedHandle, Point2<S> removedPosition)
    {
        if (_lastUsedVertex == removedHandle)
        {
            // If the last used vertex was removed, we reset to 0 or the swapped in vertex if available.
            // Since we don't know the new index of swapped in vertex easily here without more context,
            // we'll just reset to 0 for now.
            // In Rust implementation, it might be more sophisticated.
            _lastUsedVertex = new FixedVertexHandle(0);
        }
    }
}
