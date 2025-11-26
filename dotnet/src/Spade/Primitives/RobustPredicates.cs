using System;
using EA = Spade.Primitives.ExactArithmetic;

namespace Spade.Primitives
{
    internal static class RobustPredicates
    {
        private const double Epsilon = 1.1102230246251565E-16;
        private const double ResultErrBound = (3.0 + 8.0 * Epsilon) * Epsilon;
        private const double CcwErrBoundA = (3.0 + 16.0 * Epsilon) * Epsilon;
        private const double CcwErrBoundB = (2.0 + 12.0 * Epsilon) * Epsilon;
        private const double CcwErrBoundC = (9.0 + 64.0 * Epsilon) * Epsilon * Epsilon;
        private const double IccErrBoundA = (10.0 + 96.0 * Epsilon) * Epsilon;

        internal static double Orient2D(Point2<double> p1, Point2<double> p2, Point2<double> p3)
        {
            // Previous MathUtils.SideQuery logic: double-based determinant with
            // a scale-aware epsilon to classify near-collinear triples as on-line.

            double pax = p1.X;
            double pay = p1.Y;
            double pbx = p2.X;
            double pby = p2.Y;
            double pcx = p3.X;
            double pcy = p3.Y;

            double detleft = (pax - pcx) * (pby - pcy);
            double detright = (pay - pcy) * (pbx - pcx);
            double det = detleft - detright;
            double detsum;

            if (detleft > 0.0)
            {
                if (detright <= 0.0)
                {
                    return det;
                }

                detsum = detleft + detright;
            }
            else if (detleft < 0.0)
            {
                if (detright >= 0.0)
                {
                    return det;
                }

                detsum = -detleft - detright;
            }
            else
            {
                return det;
            }

            double errbound = CcwErrBoundA * detsum;
            if ((det >= errbound) || (-det >= errbound))
            {
                return det;
            }

            return Orient2DAdapt(pax, pay, pbx, pby, pcx, pcy, detsum);
        }

        private static double Orient2DAdapt(double pax, double pay, double pbx, double pby, double pcx, double pcy, double detsum)
        {
            double acx = pax - pcx;
            double bcx = pbx - pcx;
            double acy = pay - pcy;
            double bcy = pby - pcy;

            double detleft;
            double detright;
            double detlefttail;
            double detrighttail;

            double[] B = new double[4];
            double[] C1 = new double[8];
            double[] C2 = new double[12];
            double[] D = new double[16];
            double[] u = new double[4];

            double B3;
            int C1length;
            int C2length;
            int Dlength;

            double acxtail;
            double acytail;
            double bcxtail;
            double bcytail;

            double det;
            double errbound;

            EA.TwoProduct(acx, bcy, out detleft, out detlefttail);
            EA.TwoProduct(acy, bcx, out detright, out detrighttail);

            EA.TwoTwoDiff(detleft, detlefttail, detright, detrighttail, out B3, out B[2], out B[1], out B[0]);
            B[3] = B3;

            det = EA.Estimate(4, B);
            errbound = CcwErrBoundB * detsum;
            if ((det >= errbound) || (-det >= errbound))
            {
                return det;
            }

            EA.TwoDiffTail(pax, pcx, acx, out acxtail);
            EA.TwoDiffTail(pbx, pcx, bcx, out bcxtail);
            EA.TwoDiffTail(pay, pcy, acy, out acytail);
            EA.TwoDiffTail(pby, pcy, bcy, out bcytail);

            if ((acxtail == 0.0) && (acytail == 0.0) && (bcxtail == 0.0) && (bcytail == 0.0))
            {
                return det;
            }

            errbound = CcwErrBoundC * detsum + ResultErrBound * Math.Abs(det);
            det += (acx * bcytail + bcy * acxtail) - (acy * bcxtail + bcx * acytail);
            if ((det >= errbound) || (-det >= errbound))
            {
                return det;
            }

            double s1;
            double s0;
            double t1;
            double t0;
            double u3;

            EA.TwoProduct(acxtail, bcy, out s1, out s0);
            EA.TwoProduct(acytail, bcx, out t1, out t0);
            EA.TwoTwoDiff(s1, s0, t1, t0, out u3, out u[2], out u[1], out u[0]);
            u[3] = u3;
            C1length = EA.FastExpansionSumZeroElim(4, B, 4, u, C1);

            EA.TwoProduct(acx, bcytail, out s1, out s0);
            EA.TwoProduct(acy, bcxtail, out t1, out t0);
            EA.TwoTwoDiff(s1, s0, t1, t0, out u3, out u[2], out u[1], out u[0]);
            u[3] = u3;
            C2length = EA.FastExpansionSumZeroElim(C1length, C1, 4, u, C2);

            EA.TwoProduct(acxtail, bcytail, out s1, out s0);
            EA.TwoProduct(acytail, bcxtail, out t1, out t0);
            EA.TwoTwoDiff(s1, s0, t1, t0, out u3, out u[2], out u[1], out u[0]);
            u[3] = u3;
            Dlength = EA.FastExpansionSumZeroElim(C2length, C2, 4, u, D);

            return D[Dlength - 1];
        }

        internal static double Incircle(Point2<double> v1, Point2<double> v2, Point2<double> v3, Point2<double> p)
        {
            // Previous MathUtils.ContainedInCircumference determinant, now evaluated in double
            // and returned directly so the caller can decide the sign test.

            double adx = v1.X - p.X;
            double bdx = v2.X - p.X;
            double cdx = v3.X - p.X;
            double ady = v1.Y - p.Y;
            double bdy = v2.Y - p.Y;
            double cdy = v3.Y - p.Y;

            double bdxcdy = bdx * cdy;
            double cdxbdy = cdx * bdy;
            double alift = adx * adx + ady * ady;

            double cdxady = cdx * ady;
            double adxcdy = adx * cdy;
            double blift = bdx * bdx + bdy * bdy;

            double adxbdy = adx * bdy;
            double bdxady = bdx * ady;
            double clift = cdx * cdx + cdy * cdy;

            double det = alift * (bdxcdy - cdxbdy)
                       + blift * (cdxady - adxcdy)
                       + clift * (adxbdy - bdxady);

            double permanent = (Math.Abs(bdxcdy) + Math.Abs(cdxbdy)) * alift
                             + (Math.Abs(cdxady) + Math.Abs(adxcdy)) * blift
                             + (Math.Abs(adxbdy) + Math.Abs(bdxady)) * clift;
            double errbound = IccErrBoundA * permanent;
            if ((det > errbound) || (-det > errbound))
            {
                return det;
            }

            return IncircleExact(v1, v2, v3, p);
        }

        private static double IncircleExact(Point2<double> v1, Point2<double> v2, Point2<double> v3, Point2<double> p)
        {
            double pax = v1.X;
            double pay = v1.Y;
            double pbx = v2.X;
            double pby = v2.Y;
            double pcx = v3.X;
            double pcy = v3.Y;
            double pdx = p.X;
            double pdy = p.Y;

            double axby1, bxcy1, cxdy1, dxay1, axcy1, bxdy1;
            double bxay1, cxby1, dxcy1, axdy1, cxay1, dxby1;
            double axby0, bxcy0, cxdy0, dxay0, axcy0, bxdy0;
            double bxay0, cxby0, dxcy0, axdy0, cxay0, dxby0;

            double[] ab = new double[4];
            double[] bc = new double[4];
            double[] cd = new double[4];
            double[] da = new double[4];
            double[] ac = new double[4];
            double[] bd = new double[4];
            double[] temp8 = new double[8];
            int templen;
            double[] abc = new double[12];
            double[] bcd = new double[12];
            double[] cda = new double[12];
            double[] dab = new double[12];
            int abclen, bcdlen, cdalen, dablen;
            double[] det24x = new double[24];
            double[] det24y = new double[24];
            double[] det48x = new double[48];
            double[] det48y = new double[48];
            int xlen, ylen;
            double[] adet = new double[96];
            double[] bdet = new double[96];
            double[] cdet = new double[96];
            double[] ddet = new double[96];
            int alen, blen, clen, dlen;
            double[] abdet = new double[192];
            double[] cddet = new double[192];
            int ablen, cdlen;
            double[] deter = new double[384];
            int deterlen;

            int i;

            EA.TwoProduct(pax, pby, out axby1, out axby0);
            EA.TwoProduct(pbx, pay, out bxay1, out bxay0);
            EA.TwoTwoDiff(axby1, axby0, bxay1, bxay0, out ab[3], out ab[2], out ab[1], out ab[0]);

            EA.TwoProduct(pbx, pcy, out bxcy1, out bxcy0);
            EA.TwoProduct(pcx, pby, out cxby1, out cxby0);
            EA.TwoTwoDiff(bxcy1, bxcy0, cxby1, cxby0, out bc[3], out bc[2], out bc[1], out bc[0]);

            EA.TwoProduct(pcx, pdy, out cxdy1, out cxdy0);
            EA.TwoProduct(pdx, pcy, out dxcy1, out dxcy0);
            EA.TwoTwoDiff(cxdy1, cxdy0, dxcy1, dxcy0, out cd[3], out cd[2], out cd[1], out cd[0]);

            EA.TwoProduct(pdx, pay, out dxay1, out dxay0);
            EA.TwoProduct(pax, pdy, out axdy1, out axdy0);
            EA.TwoTwoDiff(dxay1, dxay0, axdy1, axdy0, out da[3], out da[2], out da[1], out da[0]);

            EA.TwoProduct(pax, pcy, out axcy1, out axcy0);
            EA.TwoProduct(pcx, pay, out cxay1, out cxay0);
            EA.TwoTwoDiff(axcy1, axcy0, cxay1, cxay0, out ac[3], out ac[2], out ac[1], out ac[0]);

            EA.TwoProduct(pbx, pdy, out bxdy1, out bxdy0);
            EA.TwoProduct(pdx, pby, out dxby1, out dxby0);
            EA.TwoTwoDiff(bxdy1, bxdy0, dxby1, dxby0, out bd[3], out bd[2], out bd[1], out bd[0]);

            templen = EA.FastExpansionSumZeroElim(4, cd, 4, da, temp8);
            cdalen = EA.FastExpansionSumZeroElim(templen, temp8, 4, ac, cda);
            templen = EA.FastExpansionSumZeroElim(4, da, 4, ab, temp8);
            dablen = EA.FastExpansionSumZeroElim(templen, temp8, 4, bd, dab);

            for (i = 0; i < 4; i++)
            {
                bd[i] = -bd[i];
                ac[i] = -ac[i];
            }

            templen = EA.FastExpansionSumZeroElim(4, ab, 4, bc, temp8);
            abclen = EA.FastExpansionSumZeroElim(templen, temp8, 4, ac, abc);
            templen = EA.FastExpansionSumZeroElim(4, bc, 4, cd, temp8);
            bcdlen = EA.FastExpansionSumZeroElim(templen, temp8, 4, bd, bcd);

            xlen = EA.ScaleExpansionZeroElim(bcdlen, bcd, pax, det24x);
            xlen = EA.ScaleExpansionZeroElim(xlen, det24x, pax, det48x);
            ylen = EA.ScaleExpansionZeroElim(bcdlen, bcd, pay, det24y);
            ylen = EA.ScaleExpansionZeroElim(ylen, det24y, pay, det48y);
            alen = EA.FastExpansionSumZeroElim(xlen, det48x, ylen, det48y, adet);

            xlen = EA.ScaleExpansionZeroElim(cdalen, cda, pbx, det24x);
            xlen = EA.ScaleExpansionZeroElim(xlen, det24x, -pbx, det48x);
            ylen = EA.ScaleExpansionZeroElim(cdalen, cda, pby, det24y);
            ylen = EA.ScaleExpansionZeroElim(ylen, det24y, -pby, det48y);
            blen = EA.FastExpansionSumZeroElim(xlen, det48x, ylen, det48y, bdet);

            xlen = EA.ScaleExpansionZeroElim(dablen, dab, pcx, det24x);
            xlen = EA.ScaleExpansionZeroElim(xlen, det24x, pcx, det48x);
            ylen = EA.ScaleExpansionZeroElim(dablen, dab, pcy, det24y);
            ylen = EA.ScaleExpansionZeroElim(ylen, det24y, pcy, det48y);
            clen = EA.FastExpansionSumZeroElim(xlen, det48x, ylen, det48y, cdet);

            xlen = EA.ScaleExpansionZeroElim(abclen, abc, pdx, det24x);
            xlen = EA.ScaleExpansionZeroElim(xlen, det24x, -pdx, det48x);
            ylen = EA.ScaleExpansionZeroElim(abclen, abc, pdy, det24y);
            ylen = EA.ScaleExpansionZeroElim(ylen, det24y, -pdy, det48y);
            dlen = EA.FastExpansionSumZeroElim(xlen, det48x, ylen, det48y, ddet);

            ablen = EA.FastExpansionSumZeroElim(alen, adet, blen, bdet, abdet);
            cdlen = EA.FastExpansionSumZeroElim(clen, cdet, dlen, ddet, cddet);
            deterlen = EA.FastExpansionSumZeroElim(ablen, abdet, cdlen, cddet, deter);

            return EA.Estimate(deterlen, deter);
        }
    }
}
