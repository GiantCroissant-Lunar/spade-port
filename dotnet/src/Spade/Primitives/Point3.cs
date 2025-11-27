using System.Numerics;

namespace Spade.Primitives;

public struct Point3<S> : IEquatable<Point3<S>>
    where S : struct, INumber<S>, ISignedNumber<S>
{
    public S X { get; }
    public S Y { get; }
    public S Z { get; }

    public Point3(S x, S y, S z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public S Distance2(Point3<S> other)
    {
        return Sub(other).Length2();
    }

    internal Point3<double> ToF64()
    {
        return new Point3<double>(double.CreateChecked(X), double.CreateChecked(Y), double.CreateChecked(Z));
    }

    internal Point3<S> Mul(S factor)
    {
        return new Point3<S>(X * factor, Y * factor, Z * factor);
    }

    internal Point3<S> Add(Point3<S> other)
    {
        return new Point3<S>(X + other.X, Y + other.Y, Z + other.Z);
    }

    internal S Length2()
    {
        return X * X + Y * Y + Z * Z;
    }

    internal Point3<S> Sub(Point3<S> other)
    {
        return new Point3<S>(X - other.X, Y - other.Y, Z - other.Z);
    }

    internal S Dot(Point3<S> other)
    {
        return X * other.X + Y * other.Y + Z * other.Z;
    }

    public bool AllComponentWise(Point3<S> other, Func<S, S, bool> f)
    {
        return f(X, other.X) && f(Y, other.Y) && f(Z, other.Z);
    }

    public override string ToString() => $"({X}, {Y}, {Z})";

    public bool Equals(Point3<S> other)
    {
        return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
    }

    public override bool Equals(object? obj)
    {
        return obj is Point3<S> other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y, Z);
    }

    public static bool operator ==(Point3<S> left, Point3<S> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Point3<S> left, Point3<S> right)
    {
        return !left.Equals(right);
    }
}
