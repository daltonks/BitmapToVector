/* Copyright (C) 2001-2019 Peter Selinger.
   Copyright (C) 2021 Dalton Spillman.
   This file is part of a C# port of Potrace. It is free software and it is covered
   by the GNU General Public License. See README.md and LICENSE for details. */

/* This header file collects some general-purpose macros (and static
   inline functions) that are used in various places. */

using System.Runtime.CompilerServices;
using point_t = BitmapToVector.Internal.PotraceInternal.point_s;
using dpoint_t = BitmapToVector.PotraceDPoint;

namespace BitmapToVector.Internal
{
    public partial class PotraceInternal
    {
        /* point arithmetic */
        internal class point_s {
            public long x;
            public long y;
        };

        /* convert point_t to dpoint_t */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static dpoint_t dpoint(point_t p) {
            dpoint_t res = new dpoint_t();
            res.X = p.x;
            res.Y = p.y;
            return res;
        }

        /* range over the straight line segment [a,b] when lambda ranges over [0,1] */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static dpoint_t interval(double lambda, dpoint_t a, dpoint_t b) {
            dpoint_t res = new dpoint_t();

            res.X = a.X + lambda * (b.X - a.X);
            res.Y = a.Y + lambda * (b.Y - a.Y);
            return res;
        }

        /* ---------------------------------------------------------------------- */
        /* some useful macros. Note: the "mod" macro works correctly for
           negative a. Also note that the test for a>=n, while redundant,
           speeds up the mod function by 70% in the average case (significant
           since the program spends about 16% of its time here - or 40%
           without the test). The "floordiv" macro returns the largest integer
           <= a/n, and again this works correctly for negative a, as long as
           a,n are integers and n>0. */

        /* integer arithmetic */
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int mod(int a, int n) {
            return a>=n ? a%n : a>=0 ? a : n-1-(-1-a)%n;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int floordiv(int a, int n) {
            return a>=0 ? a/n : -1-(-1-a)/n;
        }

        /* Note: the following work for integers and other numeric types. */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int sign(int x) => ((x)>0 ? 1 : (x)<0 ? -1 : 0);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int sign(double x) => ((x)>0 ? 1 : (x)<0 ? -1 : 0);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int abs(int a) => ((a)>0 ? (a) : -(a));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static long abs(long a) => ((a)>0 ? (a) : -(a));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int min(int a, int b) => ((a)<(b) ? (a) : (b));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int max(int a, int b) => ((a)>(b) ? (a) : (b));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int sq(int a) => ((a)*(a));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static double sq(double a) => ((a)*(a));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int cu(int a) => ((a)*(a)*(a));
    }
}
