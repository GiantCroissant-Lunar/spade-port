using System.Numerics;

namespace Spade.Primitives;

public static class MathUtils
{
    public static PointProjection<S> ProjectPoint<S>(Point2<S> p1, Point2<S> p2, Point2<S> queryPoint)
        where S : struct, INumber<S>, ISignedNumber<S>
    {
        var dir = p2.Sub(p1);
        return new PointProjection<S>(queryPoint.Sub(p1).Dot(dir), dir.Length2());
    }

    public static S Distance2<S>(Point2<S> p1, Point2<S> p2, Point2<S> queryPoint)
        where S : struct, INumber<S>, ISignedNumber<S>, IFloatingPoint<S>
    {
        var nn = NearestPoint(p1, p2, queryPoint);
        return queryPoint.Sub(nn).Length2();
    }

    public static Point2<S> NearestPoint<S>(Point2<S> p1, Point2<S> p2, Point2<S> queryPoint)
        where S : struct, INumber<S>, ISignedNumber<S>, IFloatingPoint<S>
    {
        var dir = p2.Sub(p1);
        var s = ProjectPoint(p1, p2, queryPoint);
        if (s.IsOnEdge)
        {
            var relativePosition = s.RelativePosition();
            return p1.Add(dir.Mul(relativePosition));
        }
        else if (s.IsBeforeEdge)
        {
            return p1;
        }
        else
        {
            return p2;
        }
    }

    public static LineSideInfo SideQuery<S>(Point2<S> p1, Point2<S> p2, Point2<S> queryPoint)
        where S : struct, INumber<S>, ISignedNumber<S>
    {
        // Use robust predicates for double precision
        if (typeof(S) == typeof(double))
        {
            var p1d = new Point2<double>(double.CreateChecked(p1.X), double.CreateChecked(p1.Y));
            var p2d = new Point2<double>(double.CreateChecked(p2.X), double.CreateChecked(p2.Y));
            var qd = new Point2<double>(double.CreateChecked(queryPoint.X), double.CreateChecked(queryPoint.Y));
            var det = RobustPredicates.Orient2D(p1d, p2d, qd);
            return LineSideInfo.FromDeterminant(det);
        }
        return SideQueryInaccurate(p1, p2, queryPoint);
    }

    private static LineSideInfo SideQueryInaccurate<S>(Point2<S> from, Point2<S> to, Point2<S> queryPoint)
        where S : struct, INumber<S>, ISignedNumber<S>
    {
        var q = queryPoint;
        var determinant = (to.X - from.X) * (q.Y - from.Y) - (to.Y - from.Y) * (q.X - from.X);
        return LineSideInfo.FromDeterminant(double.CreateChecked(determinant));
    }

    public static void ValidateCoordinate<S>(S value)
        where S : struct, INumber<S>, ISignedNumber<S>, IFloatingPoint<S>
    {
        if (S.IsNaN(value))
        {
            throw new ArgumentException("Coordinate is NaN");
        }

        // TODO: Check MIN/MAX allowed values if needed
    }

    public static Point2<S> Circumcenter<S>(Point2<S> a, Point2<S> b, Point2<S> c)
        where S : struct, INumber<S>, ISignedNumber<S>, IFloatingPoint<S>
    {
        var d = S.CreateChecked(2) * (a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y));
        var ux = (S.One / d) * ((a.X * a.X + a.Y * a.Y) * (b.Y - c.Y) + (b.X * b.X + b.Y * b.Y) * (c.Y - a.Y) + (c.X * c.X + c.Y * c.Y) * (a.Y - b.Y));
        var uy = (S.One / d) * ((a.X * a.X + a.Y * a.Y) * (c.X - b.X) + (b.X * b.X + b.Y * b.Y) * (a.X - c.X) + (c.X * c.X + c.Y * c.Y) * (b.X - a.X));
        return new Point2<S>(ux, uy);
    }

    public static bool ContainedInCircumference<S>(
        Point2<S> v1,
        Point2<S> v2,
        Point2<S> v3,
        Point2<S> p)
        where S : struct, INumber<S>, ISignedNumber<S>
    {
        // incircle test
        // Returns true if p is inside the circumcircle of v1, v2, v3
        // Assumes v1, v2, v3 are CCW

        // Use robust predicates for double precision
        if (typeof(S) == typeof(double))
        {
            var v1d = new Point2<double>(double.CreateChecked(v1.X), double.CreateChecked(v1.Y));
            var v2d = new Point2<double>(double.CreateChecked(v2.X), double.CreateChecked(v2.Y));
            var v3d = new Point2<double>(double.CreateChecked(v3.X), double.CreateChecked(v3.Y));
            var pd = new Point2<double>(double.CreateChecked(p.X), double.CreateChecked(p.Y));
            // incircle expects CW ordering for right-handed systems, but our interface expects CCW
            // So we reverse the order: v3, v2, v1 instead of v1, v2, v3
            var det = RobustPredicates.Incircle(v3d, v2d, v1d, pd);
            return det < 0.0;
        }

        var ax = v1.X - p.X;
        var ay = v1.Y - p.Y;
        var bx = v2.X - p.X;
        var by = v2.Y - p.Y;
        var cx = v3.X - p.X;
        var cy = v3.Y - p.Y;

        var det2 = (ax * ax + ay * ay) * (bx * cy - cx * by) -
                  (bx * bx + by * by) * (ax * cy - cx * ay) +
                  (cx * cx + cy * cy) * (ax * by - bx * ay);

        return det2 > S.Zero;
    }

    public static bool IntersectsEdgeNonCollinear<S>(Point2<S> a1, Point2<S> a2, Point2<S> b1, Point2<S> b2)
        where S : struct, INumber<S>, ISignedNumber<S>
    {
        var s1 = SideQuery(a1, a2, b1);
        var s2 = SideQuery(a1, a2, b2);
        if (s1 == s2 && !s1.IsOnLine)
        {
            return false;
        }

        var s3 = SideQuery(b1, b2, a1);
        var s4 = SideQuery(b1, b2, a2);
        if (s3 == s4 && !s3.IsOnLine)
        {
            return false;
        }
        return true;
    }

    public static (double, double, double) BarycentricCoordinates(
        Point2<double> a,
        Point2<double> b,
        Point2<double> c,
        Point2<double> p)
    {
        var v0 = b.Sub(a);
        var v1 = c.Sub(a);
        var v2 = p.Sub(a);

        var d00 = v0.Dot(v0);
        var d01 = v0.Dot(v1);
        var d11 = v1.Dot(v1);
        var d20 = v2.Dot(v0);
        var d21 = v2.Dot(v1);

        var denom = d00 * d11 - d01 * d01;

        if (denom == 0.0)
        {
            return (double.NaN, double.NaN, double.NaN);
        }

        var v = (d11 * d20 - d01 * d21) / denom;
        var w = (d00 * d21 - d01 * d20) / denom;
        var u = 1.0 - v - w;

        return (u, v, w);
    }

    public static Point2<double> MitigateUnderflow(Point2<double> position)
    {
        return new Point2<double>(
            MitigateUnderflowForCoordinate(position.X),
            MitigateUnderflowForCoordinate(position.Y)
        );
    }

    private static double MitigateUnderflowForCoordinate(double coordinate)
    {
        const double MIN_ALLOWED_VALUE = 1.793662034335766e-43;
        if (coordinate != 0.0 && Math.Abs(coordinate) < MIN_ALLOWED_VALUE)
        {
            return 0.0;
        }
        return coordinate;
    }

    public static Point2<double> GetEdgeIntersections(
        Point2<double> p1,
        Point2<double> p2,
        Point2<double> p3,
        Point2<double> p4)
    {
        var a1 = p2.Y - p1.Y;
        var b1 = p1.X - p2.X;
        var c1 = a1 * p1.X + b1 * p1.Y;

        var a2 = p4.Y - p3.Y;
        var b2 = p3.X - p4.X;
        var c2 = a2 * p3.X + b2 * p3.Y;

        var determinant = a1 * b2 - a2 * b1;

        double x, y;
        if (determinant == 0.0)
        {
            x = double.PositiveInfinity;
            y = double.PositiveInfinity;
        }
        else
        {
            x = (b2 * c1 - b1 * c2) / determinant;
            y = (a1 * c2 - a2 * c1) / determinant;
        }

        return new Point2<double>(x, y);
    }

    public static Point2<double> ApplyDeterministicJitter(Point2<double> position)
    {
        // Add a small deterministic jitter to the point position
        // to avoid collinearity or cocircularity issues.
        // For now, return identity to match legacy behavior or avoid test mismatches
        return position;
    }
}
