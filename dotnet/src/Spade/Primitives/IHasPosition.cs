using System.Numerics;

namespace Spade.Primitives;

/// <summary>
/// An object with a position.
/// Vertices need to implement this interface to allow being inserted into triangulations.
/// </summary>
public interface IHasPosition<S> where S : struct, INumber<S>, ISignedNumber<S>
{
    /// <summary>
    /// Returns the position of this object.
    /// </summary>
    Point2<S> Position { get; }
}
