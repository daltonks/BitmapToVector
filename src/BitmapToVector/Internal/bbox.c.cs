/* Copyright (C) 2001-2019 Peter Selinger.
   Copyright (C) 2021 Dalton Spillman.
   This file is part of a C# port of Potrace. It is free software and it is covered
   by the GNU General Public License. See README.md and LICENSE for details. */

using System;
using System.Runtime.CompilerServices;
using static BitmapToVector.PotraceCurve;
using interval_t = BitmapToVector.Internal.PotraceInternal.interval_s;
using dpoint_t = BitmapToVector.PotraceDPoint;
using potrace_curve_t = BitmapToVector.PotraceCurve;
using potrace_path_t = BitmapToVector.PotracePath;

namespace BitmapToVector.Internal
{
    public partial class PotraceInternal
    {
        /* ---------------------------------------------------------------------- */
        /* intervals */

        /* initialize the interval to [min, max] */
        static void interval(interval_t i, double min, double max) {
            i.min = min;
            i.max = max;
        }

        /* initialize the interval to [x, x] */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void singleton(interval_t i, double x) {
            interval(i, x, x);
        }

        /* extend the interval to include the number x */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void extend(interval_t i, double x) {
            if (x < i.min) {
                i.min = x;
            } else if (x > i.max) {
                i.max = x;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool in_interval(interval_t i, double x) {
            return i.min <= x && x <= i.max;
        }

        /* ---------------------------------------------------------------------- */
        /* inner product */

        static double iprod(dpoint_t a, dpoint_t b) {
            return a.X * b.X + a.Y * b.Y;
        }

        /* ---------------------------------------------------------------------- */
        /* linear Bezier segments */

        /* return a point on a 1-dimensional Bezier segment */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static double bezier(double t, double x0, double x1, double x2, double x3) {
            double s = 1-t;
            return s*s*s*x0 + 3*(s*s*t)*x1 + 3*(t*t*s)*x2 + t*t*t*x3;
        }

        /* Extend the interval i to include the minimum and maximum of a
           1-dimensional Bezier segment given by control points x0..x3. For
           efficiency, x0 in i is assumed as a precondition. */
        static void bezier_limits(double x0, double x1, double x2, double x3, interval_t i) {
            double a, b, c, d, r;
            double t, x;

            /* the min and max of a cubic curve segment are attained at one of
               at most 4 critical points: the 2 endpoints and at most 2 local
               extrema. We don't check the first endpoint, because all our
               curves are cyclic so it's more efficient not to check endpoints
               twice. */

            /* endpoint */
            extend(i, x3);

            /* optimization: don't bother calculating extrema if all control
               points are already in i */
            if (in_interval(i, x1) && in_interval(i, x2)) {
                return;
            }

            /* solve for extrema. at^2 + bt + c = 0 */
            a = -3*x0 + 9*x1 - 9*x2 + 3*x3;
            b = 6*x0 - 12*x1 + 6*x2;
            c = -3*x0 + 3*x1;
            d = b*b - 4*a*c;
            if (d > 0) {
                r = Math.Sqrt(d);
                t = (-b-r)/(2*a);
                if (t > 0 && t < 1) {
                    x = bezier(t, x0, x1, x2, x3);
                    extend(i, x);
                }
                t = (-b+r)/(2*a);
                if (t > 0 && t < 1) {
                    x = bezier(t, x0, x1, x2, x3);
                    extend(i, x);
                }
            }
            return;
        }

        /* ---------------------------------------------------------------------- */
        /* Potrace segments, curves, and pathlists */

        /* extend the interval i to include the inner product <v | dir> for
           all points v on the segment. Assume precondition a in i. */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void segment_limits(int tag, dpoint_t a, dpoint_t[] c, dpoint_t dir, interval_t i) {
            switch (tag) {
            case PotraceCorner:
                extend(i, iprod(c[1], dir));
                extend(i, iprod(c[2], dir));
                break;
            case PotraceCurveTo:
                bezier_limits(iprod(a, dir), iprod(c[0], dir), iprod(c[1], dir), iprod(c[2], dir), i);
                break;
            }
        }

        /* extend the interval i to include <v | dir> for all points v on the
           curve. */
        static void curve_limits(potrace_curve_t curve, dpoint_t dir, interval_t i) {
            int k;
            int n = curve.N;

            segment_limits(curve.Tag[0], curve.C[n-1][2], curve.C[0], dir, i);
            for (k=1; k<n; k++) {
                segment_limits(curve.Tag[k], curve.C[k-1][2], curve.C[k], dir, i);
            }
        }

        /* compute the interval i to be the smallest interval including all <v
           | dir> for points in the pathlist. If the pathlist is empty, return
           the singleton interval [0,0]. */
        void path_limits(potrace_path_t[] path, dpoint_t dir, interval_t i)
        {
            /* empty image? */
            if (path == null)
            {
                interval(i, 0, 0);
                return;
            }

            /* initialize interval to a point on the first curve */
            singleton(i, iprod(path[0].Curve.C[0][2], dir));

            /* iterate */
            foreach (var p in path)
            {
                curve_limits(p.Curve, dir, i);
            }
        }
    }
}
