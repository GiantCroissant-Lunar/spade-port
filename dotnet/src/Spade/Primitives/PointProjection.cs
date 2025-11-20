using System.Numerics;

namespace Spade.Primitives;

public readonly struct PointProjection<S> where S : struct, INumber<S>, ISignedNumber<S>
{
    public S Factor { get; }
    public S Length2 { get; }

    internal PointProjection(S factor, S length2)
    {
        Factor = factor;
        Length2 = length2;
    }

    public bool IsBeforeEdge => Factor < S.Zero;
    public bool IsBehindEdge => Factor > Length2;
    public bool IsOnEdge => !IsBeforeEdge && !IsBehindEdge;

    public PointProjection<S> Reversed()
    {
        return new PointProjection<S>(Length2 - Factor, Length2);
    }

    public S RelativePosition()
    {
        if (Length2 >= S.Zero)
        {
            return Factor / Length2;
        }
        else
        {
            var l = -Length2;
            var f = -Factor;
            return (l - f) / l;
        }
    }
}
