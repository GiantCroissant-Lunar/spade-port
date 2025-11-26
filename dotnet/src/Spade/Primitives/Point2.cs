using System.Numerics;

namespace Spade.Primitives;

/// <summary>
/// A two-dimensional point.
/// This is the basic type used for defining positions.
/// </summary>
public struct Point2<S> : IEquatable<Point2<S>>, IHasPosition<S>
    where S : struct, INumber<S>, ISignedNumber<S>
{
    /// <summary>
    /// The point's x coordinate
    /// </summary>
    public S X { get; }

    /// <summary>
    /// The point's y coordinate
    /// </summary>
    public S Y { get; }

    public Point2(S x, S y)
    {
        X = x;
        Y = y;
    }

    /// <summary>
    /// Returns the position of this object.
    /// </summary>
    public Point2<S> Position => this;

    /// <summary>
    /// Returns the squared distance of this point and another point.
    /// </summary>
    public S Distance2(Point2<S> other)
    {
        return Sub(other).Length2();
    }

    internal Point2<double> ToF64()
    {
        return new Point2<double>(double.CreateChecked(X), double.CreateChecked(Y));
    }

    internal Point2<S> Mul(S factor)
    {
        return new Point2<S>(X * factor, Y * factor);
    }

    internal Point2<S> Add(Point2<S> other)
    {
        return new Point2<S>(X + other.X, Y + other.Y);
    }

    internal S Length2()
    {
        return X * X + Y * Y;
    }

    internal Point2<S> Sub(Point2<S> other)
    {
        return new Point2<S>(X - other.X, Y - other.Y);
    }

    internal S Dot(Point2<S> other)
    {
        return X * other.X + Y * other.Y;
    }

    public bool AllComponentWise(Point2<S> other, Func<S, S, bool> f)
    {
        return f(X, other.X) && f(Y, other.Y);
    }

    public override string ToString() => $"({X}, {Y})";

    public bool Equals(Point2<S> other)
    {
        return X.Equals(other.X) && Y.Equals(other.Y);
    }

    public override bool Equals(object? obj)
    {
        return obj is Point2<S> other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }

    public static bool operator ==(Point2<S> left, Point2<S> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Point2<S> left, Point2<S> right)
    {
        return !left.Equals(right);
    }
}
