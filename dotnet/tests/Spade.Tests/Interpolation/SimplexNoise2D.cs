using System;

namespace Spade.Tests.Interpolation;

/// <summary>
/// Minimal 2D simplex noise implementation for tests.
/// Deterministic given a seed; outputs values in approximately [-1, 1].
/// </summary>
internal sealed class SimplexNoise2D
{
    private static readonly double F2 = 0.5 * (Math.Sqrt(3.0) - 1.0);
    private static readonly double G2 = (3.0 - Math.Sqrt(3.0)) / 6.0;

    private static readonly (double X, double Y)[] Gradients =
    {
        ( 1.0,  1.0),
        (-1.0,  1.0),
        ( 1.0, -1.0),
        (-1.0, -1.0),
        ( 1.0,  0.0),
        (-1.0,  0.0),
        ( 0.0,  1.0),
        ( 0.0, -1.0),
    };

    private readonly int[] _perm = new int[512];

    public SimplexNoise2D(int seed)
    {
        var random = new Random(seed);
        var p = new int[256];
        for (int i = 0; i < p.Length; i++)
        {
            p[i] = i;
        }

        for (int i = p.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (p[i], p[j]) = (p[j], p[i]);
        }

        for (int i = 0; i < _perm.Length; i++)
        {
            _perm[i] = p[i & 255];
        }
    }

    public double Evaluate(double x, double y)
    {
        double s = (x + y) * F2;
        int i = FastFloor(x + s);
        int j = FastFloor(y + s);

        double t = (i + j) * G2;
        double X0 = i - t;
        double Y0 = j - t;
        double x0 = x - X0;
        double y0 = y - Y0;

        int i1, j1;
        if (x0 > y0)
        {
            i1 = 1;
            j1 = 0;
        }
        else
        {
            i1 = 0;
            j1 = 1;
        }

        double x1 = x0 - i1 + G2;
        double y1 = y0 - j1 + G2;
        double x2 = x0 - 1.0 + 2.0 * G2;
        double y2 = y0 - 1.0 + 2.0 * G2;

        int ii = i & 255;
        int jj = j & 255;

        double n0 = 0.0;
        double n1 = 0.0;
        double n2 = 0.0;

        double t0 = 0.5 - x0 * x0 - y0 * y0;
        if (t0 >= 0.0)
        {
            int gi0 = _perm[ii + _perm[jj]] % Gradients.Length;
            var g0 = Gradients[gi0];
            t0 *= t0;
            n0 = t0 * t0 * (g0.X * x0 + g0.Y * y0);
        }

        double t1 = 0.5 - x1 * x1 - y1 * y1;
        if (t1 >= 0.0)
        {
            int gi1 = _perm[ii + i1 + _perm[jj + j1]] % Gradients.Length;
            var g1 = Gradients[gi1];
            t1 *= t1;
            n1 = t1 * t1 * (g1.X * x1 + g1.Y * y1);
        }

        double t2 = 0.5 - x2 * x2 - y2 * y2;
        if (t2 >= 0.0)
        {
            int gi2 = _perm[ii + 1 + _perm[jj + 1]] % Gradients.Length;
            var g2 = Gradients[gi2];
            t2 *= t2;
            n2 = t2 * t2 * (g2.X * x2 + g2.Y * y2);
        }

        return 70.0 * (n0 + n1 + n2);
    }

    private static int FastFloor(double x)
    {
        int i = (int)x;
        return x < i ? i - 1 : i;
    }
}

