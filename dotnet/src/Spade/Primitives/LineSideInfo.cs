namespace Spade.Primitives;

/// <summary>
/// Describes on which side of a line a point lies.
/// </summary>
public readonly struct LineSideInfo : IEquatable<LineSideInfo>
{
    private readonly double _signedSide;

    private LineSideInfo(double signedSide)
    {
        _signedSide = signedSide;
    }

    internal static LineSideInfo FromDeterminant(double s) => new LineSideInfo(s);

    public bool IsOnLeftSide => _signedSide > 0.0;
    public bool IsOnRightSide => _signedSide < 0.0;
    public bool IsOnLeftSideOrOnLine => _signedSide >= 0.0;
    public bool IsOnRightSideOrOnLine => _signedSide <= 0.0;
    public bool IsOnLine => Math.Abs(_signedSide) == 0.0;

    public LineSideInfo Reversed() => new LineSideInfo(-_signedSide);

    public bool Equals(LineSideInfo other)
    {
        if (IsOnLine || other.IsOnLine)
        {
            return IsOnLine && other.IsOnLine;
        }
        return IsOnRightSide == other.IsOnRightSide;
    }

    public override bool Equals(object? obj) => obj is LineSideInfo other && Equals(other);
    public override int GetHashCode() => IsOnLine ? 0 : IsOnRightSide.GetHashCode();
    public static bool operator ==(LineSideInfo left, LineSideInfo right) => left.Equals(right);
    public static bool operator !=(LineSideInfo left, LineSideInfo right) => !left.Equals(right);
}
