#region License
// License for this implementation in C#:
// --------------------------------------
//
// Copyright (c) 2012 Govert van Drimmelen
//
// This software is provided 'as-is', without any express or implied
// warranty. In no event will the authors be held liable for any damages
// arising from the use of this software.
//
// Permission is granted to anyone to use this software for any purpose,
// including commercial applications, and to alter it and redistribute it
// freely, subject to the following restrictions:
//
// 1. The origin of this software must not be misrepresented; you must not
//    claim that you wrote the original software. If you use this software
//    in a product, an acknowledgment in the product documentation would
//    be appreciated but is not required.
//
// 2. Altered source versions must be plainly marked as such, and must not
//    be misrepresented as being the original software.
//
// 3. This notice may not be removed or altered from any source distribution.
//
//
// License from original C source version:
// ---------------------------------------
//
//  Routines for Arbitrary Precision Floating-point Arithmetic
//  and Fast Robust Geometric Predicates
//  (predicates.c)
//
//  May 18, 1996
//
//  Placed in the public domain by
//  Jonathan Richard Shewchuk
//  School of Computer Science
//  Carnegie Mellon University
//  5000 Forbes Avenue
//  Pittsburgh, Pennsylvania  15213-3891
//  jrs@cs.cmu.edu
//
//  This file contains C implementation of algorithms for exact addition
//    and multiplication of floating-point numbers, and predicates for
//    robustly performing the orientation and incircle tests used in
//    computational geometry.  The algorithms and underlying theory are
//    described in Jonathan Richard Shewchuk.  "Adaptive Precision Floating-
//    Point Arithmetic and Fast Robust Geometric Predicates."  Technical
//    Report CMU-CS-96-140, School of Computer Science, Carnegie Mellon
//    University, Pittsburgh, Pennsylvania, May 1996.  (Submitted to
//    Discrete & Computational Geometry.)
//
//  This file, the paper listed above, and other information are available
//    from the Web page http://www.cs.cmu.edu/~quake/robust.html .
//
//-------------------------------------------------------------------------
#endregion

namespace Spade.Primitives;

/// <summary>
/// Implements the subset of Shewchuk-style exact floating-point arithmetic
/// needed for robust 2D orientation and incircle predicates.
/// Ported from VoronatorSharp.ExactArithmetic.
/// </summary>
internal static class ExactArithmetic
{
    #region Basic arithmetic - Sum, Diff and Product

    public static void FastTwoSum(double a, double b, out double x, out double y)
    {
        x = a + b;
        FastTwoSumTail(a, b, x, out y);
    }

    public static void FastTwoSumTail(double a, double b, double x, out double y)
    {
        double bvirt = x - a;
        y = b - bvirt;
    }

    public static void TwoSum(double a, double b, out double x, out double y)
    {
        x = a + b;
        TwoSumTail(a, b, x, out y);
    }

    public static void TwoSumTail(double a, double b, double x, out double y)
    {
        double bvirt = x - a;
        double avirt = x - bvirt;
        double bround = b - bvirt;
        double around = a - avirt;
        y = around + bround;
    }

    public static void TwoDiff(double a, double b, out double x, out double y)
    {
        x = a - b;
        TwoDiffTail(a, b, x, out y);
    }

    public static void TwoDiffTail(double a, double b, double x, out double y)
    {
        double bvirt = a - x;
        double avirt = x + bvirt;
        double bround = bvirt - b;
        double around = a - avirt;
        y = around + bround;
    }

    public static void Split(double a, out double ahi, out double alo)
    {
        const double splitter = (1 << 27) + 1.0; // 2^ceiling(p / 2) + 1 (p = 53)
        double c = splitter * a;
        double abig = c - a;
        ahi = c - abig;
        alo = a - ahi;
    }

    public static void TwoProduct(double a, double b, out double x, out double y)
    {
        x = a * b;
        TwoProductTail(a, b, x, out y);
    }

    public static void TwoProductTail(double a, double b, double x, out double y)
    {
        double ahi, alo, bhi, blo;
        Split(a, out ahi, out alo);
        Split(b, out bhi, out blo);
        double err1 = x - (ahi * bhi);
        double err2 = err1 - (alo * bhi);
        double err3 = err2 - (ahi * blo);
        y = (alo * blo) - err3;
    }

    public static void TwoProductPresplit(double a, double b, double bhi, double blo, out double x, out double y)
    {
        double ahi, alo;
        x = a * b;
        Split(a, out ahi, out alo);
        double err1 = x - (ahi * bhi);
        double err2 = err1 - (alo * bhi);
        double err3 = err2 - (ahi * blo);
        y = (alo * blo) - err3;
    }

    public static void TwoProduct2Presplit(double a, double ahi, double alo, double b, double bhi, double blo, out double x, out double y)
    {
        x = a * b;
        double err1 = x - (ahi * bhi);
        double err2 = err1 - (alo * bhi);
        double err3 = err2 - (ahi * blo);
        y = (alo * blo) - err3;
    }

    public static void Square(double a, out double x, out double y)
    {
        x = a * a;
        SquareTail(a, x, out y);
    }

    public static void SquareTail(double a, double x, out double y)
    {
        double ahi, alo;
        Split(a, out ahi, out alo);
        double err1 = x - (ahi * ahi);
        double err3 = err1 - ((ahi + ahi) * alo);
        y = (alo * alo) - err3;
    }

    #endregion

    #region Summing expansions of small fixed lengths

    public static void TwoOneSum(double a1, double a0, double b, out double x2, out double x1, out double x0)
    {
        double i;
        TwoSum(a0, b, out i, out x0);
        TwoSum(a1, i, out x2, out x1);
    }

    public static void TwoOneDiff(double a1, double a0, double b, out double x2, out double x1, out double x0)
    {
        double i;
        TwoDiff(a0, b, out i, out x0);
        TwoSum(a1, i, out x2, out x1);
    }

    public static void TwoTwoSum(double a1, double a0, double b1, double b0, out double x3, out double x2, out double x1, out double x0)
    {
        double j, _0;
        TwoOneSum(a1, a0, b0, out j, out _0, out x0);
        TwoOneSum(j, _0, b1, out x3, out x2, out x1);
    }

    public static void TwoTwoDiff(double a1, double a0, double b1, double b0, out double x3, out double x2, out double x1, out double x0)
    {
        double j, _0;
        TwoOneDiff(a1, a0, b0, out j, out _0, out x0);
        TwoOneDiff(j, _0, b1, out x3, out x2, out x1);
    }

    #endregion

    #region Expansion arithmetic - FastExpansionSumZeroElim, ScaleExpansionZeroElim, Estimate

    public static int FastExpansionSumZeroElim(int elen, double[] e, int flen, double[] f, double[] h)
    {
        double Q;
        double Qnew;
        double hh;
        int eindex, findex, hindex;
        double enow, fnow;

        enow = e[0];
        fnow = f[0];
        eindex = 0;
        findex = 0;

        if ((fnow > enow) == (fnow > -enow))
        {
            Q = enow;
            eindex++;
        }
        else
        {
            Q = fnow;
            findex++;
        }

        hindex = 0;

        if ((eindex < elen) && (findex < flen))
        {
            enow = e[eindex];
            fnow = f[findex];

            if ((fnow > enow) == (fnow > -enow))
            {
                FastTwoSum(enow, Q, out Qnew, out hh);
                eindex++;
            }
            else
            {
                FastTwoSum(fnow, Q, out Qnew, out hh);
                findex++;
            }

            Q = Qnew;
            if (hh != 0.0)
            {
                h[hindex++] = hh;
            }

            while ((eindex < elen) && (findex < flen))
            {
                enow = e[eindex];
                fnow = f[findex];

                if ((fnow > enow) == (fnow > -enow))
                {
                    TwoSum(Q, enow, out Qnew, out hh);
                    eindex++;
                }
                else
                {
                    TwoSum(Q, fnow, out Qnew, out hh);
                    findex++;
                }

                Q = Qnew;
                if (hh != 0.0)
                {
                    h[hindex++] = hh;
                }
            }
        }

        while (eindex < elen)
        {
            enow = e[eindex];
            TwoSum(Q, enow, out Qnew, out hh);
            eindex++;
            Q = Qnew;
            if (hh != 0.0)
            {
                h[hindex++] = hh;
            }
        }

        while (findex < flen)
        {
            fnow = f[findex];
            TwoSum(Q, fnow, out Qnew, out hh);
            findex++;
            Q = Qnew;
            if (hh != 0.0)
            {
                h[hindex++] = hh;
            }
        }

        if ((Q != 0.0) || (hindex == 0))
        {
            h[hindex++] = Q;
        }

        return hindex;
    }

    public static int ScaleExpansionZeroElim(int elen, double[] e, double b, double[] h)
    {
        double Q, sum;
        double hh;
        double product1;
        double product0;
        int eindex, hindex;
        double enow;
        double bhi, blo;

        Split(b, out bhi, out blo);
        TwoProductPresplit(e[0], b, bhi, blo, out Q, out hh);
        hindex = 0;
        if (hh != 0.0)
        {
            h[hindex++] = hh;
        }

        for (eindex = 1; eindex < elen; eindex++)
        {
            enow = e[eindex];
            TwoProductPresplit(enow, b, bhi, blo, out product1, out product0);
            TwoSum(Q, product0, out sum, out hh);
            if (hh != 0.0)
            {
                h[hindex++] = hh;
            }

            FastTwoSum(product1, sum, out Q, out hh);
            if (hh != 0.0)
            {
                h[hindex++] = hh;
            }
        }

        if ((Q != 0.0) || (hindex == 0))
        {
            h[hindex++] = Q;
        }

        return hindex;
    }

    public static double Estimate(int elen, double[] e)
    {
        double Q = e[0];
        for (int eindex = 1; eindex < elen; eindex++)
        {
            Q += e[eindex];
        }

        return Q;
    }

    #endregion
}
