/* Copyright (C) 2001-2019 Peter Selinger.
   Copyright (C) 2021 Dalton Spillman.
   This file is part of a C# port of Potrace. It is free software and it is covered
   by the GNU General Public License. See README.md and LICENSE for details. */

/* transform jaggy paths into smooth curves */

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BitmapToVector.Internal.Util;
using static BitmapToVector.PotraceCurve;
using point_t = BitmapToVector.Internal.PotraceInternal.point_s;
using dpoint_t = BitmapToVector.PotraceDPoint;
using privpath_t = BitmapToVector.Internal.PotraceInternal.potrace_privpath_s;
using sums_t = BitmapToVector.Internal.PotraceInternal.sums_s;
using privcurve_t = BitmapToVector.Internal.PotraceInternal.privcurve_s;
using opti_t = BitmapToVector.Internal.PotraceInternal.opti_s;
using path_t = BitmapToVector.PotracePath;
using progress_t = BitmapToVector.Internal.PotraceInternal.progress_s;

namespace BitmapToVector.Internal
{
    public partial class PotraceInternal
    {
        const int INFTY = 10000000;  /* it suffices that this is longer than any
                           path; it need not be really infinite */
        const double COS179 = -0.999847695156; /* the cosine of 179 degrees */

        /* ---------------------------------------------------------------------- */
        /* auxiliary functions */

        /* return a direction that is 90 degrees counterclockwise from p2-p0,
           but then restricted to one of the major wind directions (n, nw, w, etc) */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static point_t dorth_infty(dpoint_t p0, dpoint_t p2) {
            point_t r = new point_s();
  
            r.y = sign(p2.X-p0.X);
            r.x = -sign(p2.Y-p0.Y);

            return r;
        }

        /* return (p1-p0)x(p2-p0), the area of the parallelogram */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static double dpara(dpoint_t p0, dpoint_t p1, dpoint_t p2) {
            double x1, y1, x2, y2;

            x1 = p1.X-p0.X;
            y1 = p1.Y-p0.Y;
            x2 = p2.X-p0.X;
            y2 = p2.Y-p0.Y;

            return x1*y2 - x2*y1;
        }

        /* ddenom/dpara have the property that the square of radius 1 centered
           at p1 intersects the line p0p2 iff |dpara(p0,p1,p2)| <= ddenom(p0,p2) */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static double ddenom(dpoint_t p0, dpoint_t p2) {
            point_t r = dorth_infty(p0, p2);

            return r.y*(p2.X-p0.X) - r.x*(p2.Y-p0.Y);
        }

        /* return 1 if a <= b < c < a, in a cyclic sense (mod n) */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool cyclic(int a, int b, int c) {
            if (a<=c) {
                return (a<=b && b<c);
            } else {
                return (a<=b || b<c);
            }
        }

        /* determine the center and slope of the line i..j. Assume i<j. Needs
           "sum" components of p to be set. */
        static void pointslope(privpath_t pp, int i, int j, dpoint_t ctr, dpoint_t dir) {
            /* assume i<j */

            int n = pp.len;
            sums_t[] sums = pp.sums;

            double x, y, x2, xy, y2;
            double k;
            double a, b, c, lambda2, l;
            int r=0; /* rotations from i to j */

            while (j>=n) {
                j-=n;
                r+=1;
            }
            while (i>=n) {
                i-=n;
                r-=1;
            }
            while (j<0) {
                j+=n;
                r-=1;
            }
            while (i<0) {
                i+=n;
                r+=1;
            }
  
            x = sums[j+1].x-sums[i].x+r*sums[n].x;
            y = sums[j+1].y-sums[i].y+r*sums[n].y;
            x2 = sums[j+1].x2-sums[i].x2+r*sums[n].x2;
            xy = sums[j+1].xy-sums[i].xy+r*sums[n].xy;
            y2 = sums[j+1].y2-sums[i].y2+r*sums[n].y2;
            k = j+1-i+r*n;
  
            ctr.X = x/k;
            ctr.Y = y/k;

            a = (x2-(double)x*x/k)/k;
            b = (xy-(double)x*y/k)/k;
            c = (y2-(double)y*y/k)/k;
  
            lambda2 = (a+c+Math.Sqrt((a-c)*(a-c)+4*b*b))/2; /* larger e.value */

            /* now find e.vector for lambda2 */
            a -= lambda2;
            c -= lambda2;

            if (Math.Abs(a) >= Math.Abs(c)) {
                l = Math.Sqrt(a*a+b*b);
                if (l!=0) {
                    dir.X = -b/l;
                    dir.Y = a/l;
                }
            } else {
                l = Math.Sqrt(c*c+b*b);
                if (l!=0) {
                    dir.X = -c/l;
                    dir.Y = b/l;
                }
            }
            if (l==0) {
                dir.X = dir.Y = 0;   /* sometimes this can happen when k=4:
			      the two eigenvalues coincide */
            }
        }

        /* Apply quadratic form Q to vector w = (w.x,w.y) */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static double quadform(double[][] Q, dpoint_t w) {
            double[] v = new double[3];
            int i, j;
            double sum;

            v[0] = w.X;
            v[1] = w.Y;
            v[2] = 1;
            sum = 0.0;

            for (i=0; i<3; i++) {
                for (j=0; j<3; j++) {
                    sum += v[i] * Q[i][j] * v[j];
                }
            }
            return sum;
        }

        /* calculate p1 x p2 */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int xprod(point_t p1, point_t p2) {
            return (int) (p1.x*p2.y - p1.y*p2.x);
        }

        /* calculate (p1-p0)x(p3-p2) */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static double cprod(dpoint_t p0, dpoint_t p1, dpoint_t p2, dpoint_t p3) {
            double x1, y1, x2, y2;

            x1 = p1.X - p0.X;
            y1 = p1.Y - p0.Y;
            x2 = p3.X - p2.X;
            y2 = p3.Y - p2.Y;

            return x1*y2 - x2*y1;
        }

        /* calculate (p1-p0)*(p2-p0) */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static double iprod(dpoint_t p0, dpoint_t p1, dpoint_t p2) {
            double x1, y1, x2, y2;

            x1 = p1.X - p0.X;
            y1 = p1.Y - p0.Y;
            x2 = p2.X - p0.X;
            y2 = p2.Y - p0.Y;

            return x1*x2 + y1*y2;
        }

        /* calculate (p1-p0)*(p3-p2) */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static double iprod1(dpoint_t p0, dpoint_t p1, dpoint_t p2, dpoint_t p3) {
            double x1, y1, x2, y2;

            x1 = p1.X - p0.X;
            y1 = p1.Y - p0.Y;
            x2 = p3.X - p2.X;
            y2 = p3.Y - p2.Y;

            return x1*x2 + y1*y2;
        }

        /* calculate distance between two points */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static double ddist(dpoint_t p, dpoint_t q) {
            return Math.Sqrt(sq(p.X-q.X)+sq(p.Y-q.Y));
        }

        /* calculate point of a bezier curve */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static dpoint_t bezier(double t, dpoint_t p0, dpoint_t p1, dpoint_t p2, dpoint_t p3) {
            double s = 1-t;
            dpoint_t res = new dpoint_t();

            /* Note: a good optimizing compiler (such as gcc-3) reduces the
               following to 16 multiplications, using common subexpression
               elimination. */

            res.X = s*s*s*p0.X + 3*(s*s*t)*p1.X + 3*(t*t*s)*p2.X + t*t*t*p3.X;
            res.Y = s*s*s*p0.Y + 3*(s*s*t)*p1.Y + 3*(t*t*s)*p2.Y + t*t*t*p3.Y;

            return res;
        }

        /* calculate the point t in [0..1] on the (convex) bezier curve
           (p0,p1,p2,p3) which is tangent to q1-q0. Return -1.0 if there is no
           solution in [0..1]. */
        static double tangent(dpoint_t p0, dpoint_t p1, dpoint_t p2, dpoint_t p3, dpoint_t q0, dpoint_t q1) {
            double A, B, C;   /* (1-t)^2 A + 2(1-t)t B + t^2 C = 0 */
            double a, b, c;   /* a t^2 + b t + c = 0 */
            double d, s, r1, r2;

            A = cprod(p0, p1, q0, q1);
            B = cprod(p1, p2, q0, q1);
            C = cprod(p2, p3, q0, q1);

            a = A - 2*B + C;
            b = -2*A + 2*B;
            c = A;
  
            d = b*b - 4*a*c;

            if (a==0 || d<0) {
                return -1.0;
            }

            s = Math.Sqrt(d);

            r1 = (-b + s) / (2 * a);
            r2 = (-b - s) / (2 * a);

            if (r1 >= 0 && r1 <= 1) {
                return r1;
            } else if (r2 >= 0 && r2 <= 1) {
                return r2;
            } else {
                return -1.0;
            }
        }

        /* ---------------------------------------------------------------------- */
        /* Preparation: fill in the sum* fields of a path (used for later
           rapid summing). Return 0 on success, 1 with errno set on
           failure. */
        static int calc_sums(privpath_t pp) {
            int i, x, y;
            int n = pp.len;

            pp.sums = new sums_t[pp.len + 1].SetAll(() => new sums_t());

            /* origin */
            pp.x0 = (int) pp.pt[0].x;
            pp.y0 = (int) pp.pt[0].y;

            /* preparatory computation for later fast summing */
            pp.sums[0].x2 = pp.sums[0].xy = pp.sums[0].y2 = pp.sums[0].x = pp.sums[0].y = 0;
            for (i=0; i<n; i++) {
                x = (int) (pp.pt[i].x - pp.x0);
                y = (int) (pp.pt[i].y - pp.y0);
                pp.sums[i+1].x = pp.sums[i].x + x;
                pp.sums[i+1].y = pp.sums[i].y + y;
                pp.sums[i+1].x2 = pp.sums[i].x2 + (double)x*x;
                pp.sums[i+1].xy = pp.sums[i].xy + (double)x*y;
                pp.sums[i+1].y2 = pp.sums[i].y2 + (double)y*y;
            }
            return 0;
        }

        /* ---------------------------------------------------------------------- */
        /* Stage 1: determine the straight subpaths (Sec. 2.2.1). Fill in the
           "lon" component of a path object (based on pt/len).	For each i,
           lon[i] is the furthest index such that a straight line can be drawn
           from i to lon[i]. Return 1 on error with errno set, else 0. */

        /* this algorithm depends on the fact that the existence of straight
           subpaths is a triplewise property. I.e., there exists a straight
           line through squares i0,...,in iff there exists a straight line
           through i,j,k, for all i0<=i<j<k<=in. (Proof?) */

        /* this implementation of calc_lon is O(n^2). It replaces an older
           O(n^3) version. A "constraint" means that future points must
           satisfy xprod(constraint[0], cur) >= 0 and xprod(constraint[1],
           cur) <= 0. */

        /* Remark for Potrace 1.1: the current implementation of calc_lon is
           more complex than the implementation found in Potrace 1.0, but it
           is considerably faster. The introduction of the "nc" data structure
           means that we only have to test the constraints for "corner"
           points. On a typical input file, this speeds up the calc_lon
           function by a factor of 31.2, thereby decreasing its time share
           within the overall Potrace algorithm from 72.6% to 7.82%, and
           speeding up the overall algorithm by a factor of 3.36. On another
           input file, calc_lon was sped up by a factor of 6.7, decreasing its
           time share from 51.4% to 13.61%, and speeding up the overall
           algorithm by a factor of 1.78. In any case, the savings are
           substantial. */

        /* returns 0 on success, 1 on error with errno set */
        static int calc_lon(privpath_t pp) {
            List<point_t> pt = pp.pt;
            int n = pp.len;
            int i, j, k, k1;
            int[] ct = new int[4]; int dir;
            point_t[] constraint = new point_t[2].SetAll(() => new point_t());
            point_t cur = new point_t();
            point_t off = new point_t();
            int[] pivk = new int[n];  /* pivk[n] */
            int[] nc = new int[n];    /* nc[n]: next corner */
            point_t dk = new point_t();  /* direction of k-k1 */
            int a, b, c, d;

            /* initialize the nc data structure. Point from each point to the
               furthest future point to which it is connected by a vertical or
               horizontal segment. We take advantage of the fact that there is
               always a direction change at 0 (due to the path decomposition
               algorithm). But even if this were not so, there is no harm, as
               in practice, correctness does not depend on the word "furthest"
               above.  */
            k = 0;
            for (i=n-1; i>=0; i--) {
                if (pt[i].x != pt[k].x && pt[i].y != pt[k].y) {
                    k = i+1;  /* necessarily i<n-1 in this case */
                }
                nc[i] = k;
            }

            pp.lon = new int[n];

            /* determine pivot points: for each i, let pivk[i] be the furthest k
               such that all j with i<j<k lie on a line connecting i,k. */
            
            for (i=n-1; i>=0; i--) {
                ct[0] = ct[1] = ct[2] = ct[3] = 0;

                /* keep track of "directions" that have occurred */
                dir = (int) ((3+3*(pt[mod(i+1,n)].x-pt[i].x)+(pt[mod(i+1,n)].y-pt[i].y))/2);
                ct[dir]++;

                constraint[0].x = 0;
                constraint[0].y = 0;
                constraint[1].x = 0;
                constraint[1].y = 0;

                /* find the next k such that no straight line from i to k */
                k = nc[i];
                k1 = i;
                while (true) {
                  
                    dir = (3+3*sign(pt[k].x-pt[k1].x)+sign(pt[k].y-pt[k1].y))/2;
                    ct[dir]++;

                    /* if all four "directions" have occurred, cut this path */
                    if (ct[0] != 0 && ct[1] != 0 && ct[2] != 0 && ct[3] != 0) {
	                    pivk[i] = k1;
	                    goto foundk;
                    }

                    cur.x = pt[k].x - pt[i].x;
                    cur.y = pt[k].y - pt[i].y;

                    /* see if current constraint is violated */
                    if (xprod(constraint[0], cur) < 0 || xprod(constraint[1], cur) > 0) {
	                    goto constraint_viol;
                    }

                    /* else, update constraint */
                    if (abs(cur.x) <= 1 && abs(cur.y) <= 1) { 
                        /* no constraint */
                    } else {
	                    off.x = cur.x + ((cur.y>=0 && (cur.y>0 || cur.x<0)) ? 1 : -1);
	                    off.y = cur.y + ((cur.x<=0 && (cur.x<0 || cur.y<0)) ? 1 : -1);
	                    if (xprod(constraint[0], off) >= 0) {
	                      constraint[0] = off;
	                    }
	                    off.x = cur.x + ((cur.y<=0 && (cur.y<0 || cur.x<0)) ? 1 : -1);
	                    off.y = cur.y + ((cur.x>=0 && (cur.x>0 || cur.y<0)) ? 1 : -1);
	                    if (xprod(constraint[1], off) <= 0) {
	                      constraint[1] = off;
	                    }
                    }	
                    k1 = k;
                    k = nc[k1];
                    if (!cyclic(k,i,k1)) {
	                    break;
                    }
                }
                constraint_viol:
                    /* k1 was the last "corner" satisfying the current constraint, and
                       k is the first one violating it. We now need to find the last
                       point along k1..k which satisfied the constraint. */
                    dk.x = sign(pt[k].x-pt[k1].x);
                    dk.y = sign(pt[k].y-pt[k1].y);
                    cur.x = pt[k1].x - pt[i].x;
                    cur.y = pt[k1].y - pt[i].y;
                    /* find largest integer j such that xprod(constraint[0], cur+j*dk)
                       >= 0 and xprod(constraint[1], cur+j*dk) <= 0. Use bilinearity
                       of xprod. */
                    a = xprod(constraint[0], cur);
                    b = xprod(constraint[0], dk);
                    c = xprod(constraint[1], cur);
                    d = xprod(constraint[1], dk);
                    /* find largest integer j such that a+j*b>=0 and c+j*d<=0. This
                       can be solved with integer arithmetic. */
                    j = INFTY;
                    if (b<0) {
                        j = floordiv(a,-b);
                    }
                    if (d>0) {
                        j = min(j, floordiv(-c,d));
                    }
                    pivk[i] = mod(k1+j,n);
                foundk:
                ;
            } /* for i */

            /* clean up: for each i, let lon[i] be the largest k such that for
               all i' with i<=i'<k, i'<k<=pivk[i']. */

            j=pivk[n-1];
            pp.lon[n-1]=j;
            for (i=n-2; i>=0; i--) {
                if (cyclic(i+1,pivk[i],j)) {
                    j=pivk[i];
                }
                pp.lon[i]=j;
            }

            for (i=n-1; cyclic(mod(i+1,n),j,pp.lon[i]); i--) {
                pp.lon[i] = j;
            }
            
            return 0;
        }

        /* ---------------------------------------------------------------------- */
        /* Stage 2: calculate the optimal polygon (Sec. 2.2.2-2.2.4). */ 

        /* Auxiliary function: calculate the penalty of an edge from i to j in
           the given path. This needs the "lon" and "sum*" data. */

        static double penalty3(privpath_t pp, int i, int j) {
            int n = pp.len;
            List<point_t> pt = pp.pt;
            sums_t[] sums = pp.sums;

            /* assume 0<=i<j<=n  */
            double x, y, x2, xy, y2;
            double k;
            double a, b, c, s;
            double px, py, ex, ey;

            int r = 0; /* rotations from i to j */

            if (j>=n) {
                j -= n;
                r = 1;
            }
  
            /* critical inner loop: the "if" gives a 4.6 percent speedup */
            if (r == 0) {
                x = sums[j+1].x - sums[i].x;
                y = sums[j+1].y - sums[i].y;
                x2 = sums[j+1].x2 - sums[i].x2;
                xy = sums[j+1].xy - sums[i].xy;
                y2 = sums[j+1].y2 - sums[i].y2;
                k = j+1 - i;
            } else {
                x = sums[j+1].x - sums[i].x + sums[n].x;
                y = sums[j+1].y - sums[i].y + sums[n].y;
                x2 = sums[j+1].x2 - sums[i].x2 + sums[n].x2;
                xy = sums[j+1].xy - sums[i].xy + sums[n].xy;
                y2 = sums[j+1].y2 - sums[i].y2 + sums[n].y2;
                k = j+1 - i + n;
            } 

            px = (pt[i].x + pt[j].x) / 2.0 - pt[0].x;
            py = (pt[i].y + pt[j].y) / 2.0 - pt[0].y;
            ey = (pt[j].x - pt[i].x);
            ex = -(pt[j].y - pt[i].y);

            a = ((x2 - 2*x*px) / k + px*px);
            b = ((xy - x*py - y*px) / k + px*py);
            c = ((y2 - 2*y*py) / k + py*py);
  
            s = ex*ex*a + 2*ex*ey*b + ey*ey*c;

            return Math.Sqrt(s);
        }

        /* find the optimal polygon. Fill in the m and po components. Return 1
           on failure with errno set, else 0. Non-cyclic version: assumes i=0
           is in the polygon. Fixme: implement cyclic version. */
        static int bestpolygon(privpath_t pp)
        {
            int i, j, m, k;     
            int n = pp.len;
            double[] pen = null; /* pen[n+1]: penalty vector */
            int[] prev = null;   /* prev[n+1]: best path pointer vector */
            int[] clip0 = null;  /* clip0[n]: longest segment pointer, non-cyclic */
            int[] clip1 = null;  /* clip1[n+1]: backwards segment pointer, non-cyclic */
            int[] seg0 = null;    /* seg0[m+1]: forward segment bounds, m<=n */
            int[] seg1 = null;   /* seg1[m+1]: backward segment bounds, m<=n */
            double thispen;
            double best;
            int c;

            pen = new double[n+1];
            prev = new int[n+1];
            clip0 = new int[n];
            clip1 = new int[n+1];
            seg0 = new int[n+1];
            seg1 = new int[n+1];

            /* calculate clipped paths */
            for (i=0; i<n; i++) {
                c = mod(pp.lon[mod(i-1,n)]-1,n);
                if (c == i) {
                    c = mod(i+1,n);
                }
                if (c < i) {
                    clip0[i] = n;
                } else {
                    clip0[i] = c;
                }
            }

            /* calculate backwards path clipping, non-cyclic. j <= clip0[i] iff
               clip1[j] <= i, for i,j=0..n. */
            j = 1;
            for (i=0; i<n; i++) {
                while (j <= clip0[i]) {
                    clip1[j] = i;
                    j++;
                }
            }

            /* calculate seg0[j] = longest path from 0 with j segments */
            i = 0;
            for (j=0; i<n; j++) {
                seg0[j] = i;
                i = clip0[i];
            }
            seg0[j] = n;
            m = j;

            /* calculate seg1[j] = longest path to n with m-j segments */
            i = n;
            for (j=m; j>0; j--) {
                seg1[j] = i;
                i = clip1[i];
            }
            seg1[0] = 0;

            /* now find the shortest path with m segments, based on penalty3 */
            /* note: the outer 2 loops jointly have at most n iterations, thus
               the worst-case behavior here is quadratic. In practice, it is
               close to linear since the inner loop tends to be short. */
            pen[0]=0;
            for (j=1; j<=m; j++) {
                for (i=seg1[j]; i<=seg0[j]; i++) {
                    best = -1;
                    for (k=seg0[j-1]; k>=clip1[i]; k--) {
	                    thispen = penalty3(pp, k, i) + pen[k];
	                    if (best < 0 || thispen < best) {
	                        prev[i] = k;
	                        best = thispen;
	                    }
                    }
                    pen[i] = best;
                }
            }

            pp.m = m;
            pp.po = new int[m];

            /* read off shortest path */
            for (i=n, j=m-1; i>0; j--) {
                i = prev[i];
                pp.po[j] = i;
            }
            
            return 0;
        }

        /* ---------------------------------------------------------------------- */
        /* Stage 3: vertex adjustment (Sec. 2.3.1). */

        /* Adjust vertices of optimal polygon: calculate the intersection of
           the two "optimal" line segments, then move it into the unit square
           if it lies outside. Return 1 with errno set on error; 0 on
           success. */

        static int adjust_vertices(privpath_t pp) {
            int m = pp.m;
            int[] po = pp.po;
            int n = pp.len;
            List<point_t> pt = pp.pt;
            int x0 = pp.x0;
            int y0 = pp.y0;
            
            dpoint_t[] ctr = null;      /* ctr[m] */
            dpoint_t[] dir = null;      /* dir[m] */
            double[][][] q = null;      /* q[m] */
            double[] v = new double[3];
            double d;
            int i, j, k, l;
            dpoint_t s = new dpoint_t();
            int r;
            
            ctr = new dpoint_t[m].SetAll(() => new dpoint_t());
            dir = new dpoint_t[m].SetAll(() => new dpoint_t());
            q = new double[m][][].SetAll(() => new double[3][].SetAll(() => new double[3]));
            
            r = privcurve_init(pp.curve, m);
            
            /* calculate "optimal" point-slope representation for each line
               segment */
            for (i=0; i<m; i++) {
                j = po[mod(i+1,m)];
                j = mod(j-po[i],n)+po[i];
                pointslope(pp, po[i], j, ctr[i], dir[i]);
            }
            
            /* represent each line segment as a singular quadratic form; the
               distance of a point (x,y) from the line segment will be
               (x,y,1)Q(x,y,1)^t, where Q=q[i]. */
            for (i=0; i<m; i++) {
                d = sq(dir[i].X) + sq(dir[i].Y);
                if (d == 0.0) {
                    for (j=0; j<3; j++) {
	                    for (k=0; k<3; k++) {
	                        q[i][j][k] = 0;
	                    }
                    }
                } else {
                    v[0] = dir[i].Y;
                    v[1] = -dir[i].X;
                    v[2] = - v[1] * ctr[i].Y - v[0] * ctr[i].X;
                    for (l=0; l<3; l++) {
	                    for (k=0; k<3; k++) {
	                        q[i][l][k] = v[l] * v[k] / d;
	                    }
                    }
                }
            }
            
            /* now calculate the "intersections" of consecutive segments.
               Instead of using the actual intersection, we find the point
               within a given unit square which minimizes the square distance to
               the two lines. */
            for (i=0; i<m; i++) {
                double[][] Q = new double[3][].SetAll(() => new double[3]);
                dpoint_t w = new dpoint_t();
                double dx, dy;
                double det;
                double min, cand; /* minimum and candidate for minimum of quad. form */
                double xmin, ymin;	/* coordinates of minimum */
                int z;
                
                /* let s be the vertex, in coordinates relative to x0/y0 */
                s.X = pt[po[i]].x-x0;
                s.Y = pt[po[i]].y-y0;
                
                /* intersect segments i-1 and i */
                
                j = mod(i-1,m);
                
                /* add quadratic forms */
                for (l=0; l<3; l++) {
                    for (k=0; k<3; k++) {
	                    Q[l][k] = q[j][l][k] + q[i][l][k];
                    }
                }
                
                while(true) {
                    /* minimize the quadratic form Q on the unit square */
                    /* find intersection */
                
                    det = Q[0][0]*Q[1][1] - Q[0][1]*Q[1][0];
                    if (det != 0.0) {
	                    w.X = (-Q[0][2]*Q[1][1] + Q[1][2]*Q[0][1]) / det;
	                    w.Y = ( Q[0][2]*Q[1][0] - Q[1][2]*Q[0][0]) / det;
	                    break;
                    }
                
                    /* matrix is singular - lines are parallel. Add another,
	                orthogonal axis, through the center of the unit square */
                    if (Q[0][0]>Q[1][1]) {
	                    v[0] = -Q[0][1];
	                    v[1] = Q[0][0];
                    } else if (Q[1][1] != 0) {
	                    v[0] = -Q[1][1];
	                    v[1] = Q[1][0];
                    } else {
	                    v[0] = 1;
	                    v[1] = 0;
                    }
                    d = sq(v[0]) + sq(v[1]);
                    v[2] = - v[1] * s.Y - v[0] * s.X;
                    for (l=0; l<3; l++) {
	                    for (k=0; k<3; k++) {
	                        Q[l][k] += v[l] * v[k] / d;
	                    }
                    }
                }
                dx = Math.Abs(w.X-s.X);
                dy = Math.Abs(w.Y-s.Y);
                if (dx <= .5 && dy <= .5) {
                    pp.curve.vertex[i].X = w.X+x0;
                    pp.curve.vertex[i].Y = w.Y+y0;
                    continue;
                }
                
                /* the minimum was not in the unit square; now minimize quadratic
                   on boundary of square */
                min = quadform(Q, s);
                xmin = s.X;
                ymin = s.Y;
                
                if (Q[0][0] == 0.0) {
                    goto fixx;
                }
                for (z=0; z<2; z++) {   /* value of the y-coordinate */
                    w.Y = s.Y-0.5+z;
                    w.X = - (Q[0][1] * w.Y + Q[0][2]) / Q[0][0];
                    dx = Math.Abs(w.X-s.X);
                    cand = quadform(Q, w);
                    if (dx <= .5 && cand < min) {
	                    min = cand;
	                    xmin = w.X;
	                    ymin = w.Y;
                    } 
                }
                fixx:
                if (Q[1][1] == 0.0) {
                    goto corners;
                }
                for (z=0; z<2; z++) {   /* value of the x-coordinate */
                    w.X = s.X-0.5+z;
                    w.Y = - (Q[1][0] * w.X + Q[1][2]) / Q[1][1];
                    dy = Math.Abs(w.Y-s.Y);
                    cand = quadform(Q, w);
                    if (dy <= .5 && cand < min) {
	                    min = cand;
	                    xmin = w.X;
	                    ymin = w.Y;
                    }
                }
                corners:
                /* check four corners */
                for (l=0; l<2; l++) {
                    for (k=0; k<2; k++) {
	                    w.X = s.X-0.5+l;
	                    w.Y = s.Y-0.5+k;
	                    cand = quadform(Q, w);
	                    if (cand < min) {
	                      min = cand;
	                      xmin = w.X;
	                      ymin = w.Y;
	                    }
                    }
                }
                
                pp.curve.vertex[i].X = xmin + x0;
                pp.curve.vertex[i].Y = ymin + y0;
                continue;
            }
            
            return 0;
        }

        /* ---------------------------------------------------------------------- */
        /* Stage 4: smoothing and corner analysis (Sec. 2.3.3) */

        /* reverse orientation of a path */
        static void reverse(privcurve_t curve) {
            int m = curve.n;
            int i, j;
            dpoint_t tmp;

            for (i=0, j=m-1; i<j; i++, j--) {
                tmp = curve.vertex[i];
                curve.vertex[i] = curve.vertex[j];
                curve.vertex[j] = tmp;
            }
        }

        /* Always succeeds */
        static void smooth(privcurve_t curve, double alphamax) {
            int m = curve.n;

            int i, j, k;
            double dd, denom, alpha;
            dpoint_t p2, p3, p4;

            /* examine each vertex and find its best fit */
            for (i=0; i<m; i++) {
                j = mod(i+1, m);
                k = mod(i+2, m);
                p4 = interval(1/2.0, curve.vertex[k], curve.vertex[j]);

                denom = ddenom(curve.vertex[i], curve.vertex[k]);
                if (denom != 0.0) {
                    dd = dpara(curve.vertex[i], curve.vertex[j], curve.vertex[k]) / denom;
                    dd = Math.Abs(dd);
                    alpha = dd>1 ? (1 - 1.0/dd) : 0;
                    alpha = alpha / 0.75;
                } else {
                    alpha = 4/3.0;
                }
                curve.alpha0[j] = alpha;	 /* remember "original" value of alpha */

                if (alpha >= alphamax) {  /* pointed corner */
                    curve.tag[j] = PotraceCorner;
                    curve.c[j][1] = curve.vertex[j];
                    curve.c[j][2] = p4;
                } else {
                    if (alpha < 0.55) {
                        alpha = 0.55;
                    } else if (alpha > 1) {
                        alpha = 1;
                    }
                    p2 = interval(.5+.5*alpha, curve.vertex[i], curve.vertex[j]);
                    p3 = interval(.5+.5*alpha, curve.vertex[k], curve.vertex[j]);
                    curve.tag[j] = PotraceCurveTo;
                    curve.c[j][0] = p2;
                    curve.c[j][1] = p3;
                    curve.c[j][2] = p4;
                }
                curve.alpha[j] = alpha;	/* store the "cropped" value of alpha */
                curve.beta[j] = 0.5;
            }
            curve.alphacurve = 1;

            return;
        }

        /* ---------------------------------------------------------------------- */
        /* Stage 5: Curve optimization (Sec. 2.4) */

        /* a private type for the result of opti_penalty */
        internal class opti_s {
            public double pen;	   /* penalty */
            public dpoint_t[] c = new dpoint_t[2].SetAll(() => new dpoint_t());   /* curve parameters */
            public double t, s;	   /* curve parameters */
            public double alpha;	   /* curve parameter */
        };

        /* calculate best fit from i+.5 to j+.5.  Assume i<j (cyclically).
           Return 0 and set badness and parameters (alpha, beta), if
           possible. Return 1 if impossible. */
        static int opti_penalty(privpath_t pp, int i, int j, opti_t res, double opttolerance, int[] convc, double[] areac) {
          int m = pp.curve.n;
          int k, k1, k2, conv, i1;
          double area, alpha, d, d1, d2;
          dpoint_t p0, p1, p2, p3, pt;
          double A, R, A1, A2, A3, A4;
          double s, t;

          /* check convexity, corner-freeness, and maximum bend < 179 degrees */

          if (i==j) {  /* sanity - a full loop can never be an opticurve */
            return 1;
          }

          k = i;
          i1 = mod(i+1, m);
          k1 = mod(k+1, m);
          conv = convc[k1];
          if (conv == 0) {
            return 1;
          }
          d = ddist(pp.curve.vertex[i], pp.curve.vertex[i1]);
          for (k=k1; k!=j; k=k1) {
            k1 = mod(k+1, m);
            k2 = mod(k+2, m);
            if (convc[k1] != conv) {
              return 1;
            }
            if (sign(cprod(pp.curve.vertex[i], pp.curve.vertex[i1], pp.curve.vertex[k1], pp.curve.vertex[k2])) != conv) {
              return 1;
            }
            if (iprod1(pp.curve.vertex[i], pp.curve.vertex[i1], pp.curve.vertex[k1], pp.curve.vertex[k2]) < d * ddist(pp.curve.vertex[k1], pp.curve.vertex[k2]) * COS179) {
              return 1;
            }
          }

          /* the curve we're working in: */
          p0 = pp.curve.c[mod(i,m)][2];
          p1 = pp.curve.vertex[mod(i+1,m)];
          p2 = pp.curve.vertex[mod(j,m)];
          p3 = pp.curve.c[mod(j,m)][2];

          /* determine its area */
          area = areac[j] - areac[i];
          area -= dpara(pp.curve.vertex[0], pp.curve.c[i][2], pp.curve.c[j][2])/2;
          if (i>=j) {
            area += areac[m];
          }

          /* find intersection o of p0p1 and p2p3. Let t,s such that o =
             interval(t,p0,p1) = interval(s,p3,p2). Let A be the area of the
             triangle (p0,o,p3). */

          A1 = dpara(p0, p1, p2);
          A2 = dpara(p0, p1, p3);
          A3 = dpara(p0, p2, p3);
          /* A4 = dpara(p1, p2, p3); */
          A4 = A1+A3-A2;    
          
          if (A2 == A1) {  /* this should never happen */
            return 1;
          }

          t = A3/(A3-A4);
          s = A2/(A2-A1);
          A = A2 * t / 2.0;
          
          if (A == 0.0) {  /* this should never happen */
            return 1;
          }

          R = area / A;	 /* relative area */
          alpha = 2 - Math.Sqrt(4 - R / 0.3);  /* overall alpha for p0-o-p3 curve */

          res.c[0] = interval(t * alpha, p0, p1);
          res.c[1] = interval(s * alpha, p3, p2);
          res.alpha = alpha;
          res.t = t;
          res.s = s;

          p1 = res.c[0];
          p2 = res.c[1];  /* the proposed curve is now (p0,p1,p2,p3) */

          res.pen = 0;

          /* calculate penalty */
          /* check tangency with edges */
          for (k=mod(i+1,m); k!=j; k=k1) {
            k1 = mod(k+1,m);
            t = tangent(p0, p1, p2, p3, pp.curve.vertex[k], pp.curve.vertex[k1]);
            if (t<-.5) {
              return 1;
            }
            pt = bezier(t, p0, p1, p2, p3);
            d = ddist(pp.curve.vertex[k], pp.curve.vertex[k1]);
            if (d == 0.0) {  /* this should never happen */
              return 1;
            }
            d1 = dpara(pp.curve.vertex[k], pp.curve.vertex[k1], pt) / d;
            if (Math.Abs(d1) > opttolerance) {
              return 1;
            }
            if (iprod(pp.curve.vertex[k], pp.curve.vertex[k1], pt) < 0 || iprod(pp.curve.vertex[k1], pp.curve.vertex[k], pt) < 0) {
              return 1;
            }
            res.pen += sq(d1);
          }

          /* check corners */
          for (k=i; k!=j; k=k1) {
            k1 = mod(k+1,m);
            t = tangent(p0, p1, p2, p3, pp.curve.c[k][2], pp.curve.c[k1][2]);
            if (t<-.5) {
              return 1;
            }
            pt = bezier(t, p0, p1, p2, p3);
            d = ddist(pp.curve.c[k][2], pp.curve.c[k1][2]);
            if (d == 0.0) {  /* this should never happen */
              return 1;
            }
            d1 = dpara(pp.curve.c[k][2], pp.curve.c[k1][2], pt) / d;
            d2 = dpara(pp.curve.c[k][2], pp.curve.c[k1][2], pp.curve.vertex[k1]) / d;
            d2 *= 0.75 * pp.curve.alpha[k1];
            if (d2 < 0) {
              d1 = -d1;
              d2 = -d2;
            }
            if (d1 < d2 - opttolerance) {
              return 1;
            }
            if (d1 < d2) {
              res.pen += sq(d1 - d2);
            }
          }

          return 0;
        }

        /* optimize the path p, replacing sequences of Bezier segments by a
           single segment when possible. Return 0 on success, 1 with errno set
           on failure. */
        static int opticurve(privpath_t pp, double opttolerance) {
          int m = pp.curve.n;
          int[] pt = null;     /* pt[m+1] */
          double[] pen = null; /* pen[m+1] */
          int[] len = null;    /* len[m+1] */
          opti_t[] opt = null; /* opt[m+1] */
          int om;
          int i,j,r;
          opti_t o = new opti_t();
          dpoint_t p0 = new dpoint_t();
          int i1;
          double area;
          double alpha;
          double[] s = null;
          double[] t = null;

          int[] convc = null; /* conv[m]: pre-computed convexities */
          double[] areac = null; /* cumarea[m+1]: cache for fast area computation */

          pt = new int[m+1];
          pen = new double[m+1];
          len = new int[m+1];
          opt = new opti_s[m+1];
          convc = new int[m];
          areac = new double[m+1];

          /* pre-calculate convexity: +1 = right turn, -1 = left turn, 0 = corner */
          for (i=0; i<m; i++) {
            if (pp.curve.tag[i] == PotraceCurveTo) {
              convc[i] = sign(dpara(pp.curve.vertex[mod(i-1,m)], pp.curve.vertex[i], pp.curve.vertex[mod(i+1,m)]));
            } else {
              convc[i] = 0;
            }
          }

          /* pre-calculate areas */
          area = 0.0;
          areac[0] = 0.0;
          p0 = pp.curve.vertex[0];
          for (i=0; i<m; i++) {
            i1 = mod(i+1, m);
            if (pp.curve.tag[i1] == PotraceCurveTo) {
              alpha = pp.curve.alpha[i1];
              area += 0.3*alpha*(4-alpha)*dpara(pp.curve.c[i][2], pp.curve.vertex[i1], pp.curve.c[i1][2])/2;
              area += dpara(p0, pp.curve.c[i][2], pp.curve.c[i1][2])/2;
            }
            areac[i+1] = area;
          }

          pt[0] = -1;
          pen[0] = 0;
          len[0] = 0;

          /* Fixme: we always start from a fixed point -- should find the best
             curve cyclically */

          for (j=1; j<=m; j++) {
            /* calculate best path from 0 to j */
            pt[j] = j-1;
            pen[j] = pen[j-1];
            len[j] = len[j-1]+1;

            for (i=j-2; i>=0; i--) {
              r = opti_penalty(pp, i, mod(j,m), o, opttolerance, convc, areac);
              if (r != 0) {
	            break;
              }
              if (len[j] > len[i]+1 || (len[j] == len[i]+1 && pen[j] > pen[i] + o.pen)) {
	            pt[j] = i;
	            pen[j] = pen[i] + o.pen;
	            len[j] = len[i] + 1;
	            opt[j] = o;
              }
            }
          }
          om = len[m];
          r = privcurve_init(pp.ocurve, om);
          s = new double[om];
          t = new double[om];

          j = m;
          for (i=om-1; i>=0; i--) {
            if (pt[j]==j-1) {
              pp.ocurve.tag[i]     = pp.curve.tag[mod(j,m)];
              pp.ocurve.c[i][0]    = pp.curve.c[mod(j,m)][0];
              pp.ocurve.c[i][1]    = pp.curve.c[mod(j,m)][1];
              pp.ocurve.c[i][2]    = pp.curve.c[mod(j,m)][2];
              pp.ocurve.vertex[i]  = pp.curve.vertex[mod(j,m)];
              pp.ocurve.alpha[i]   = pp.curve.alpha[mod(j,m)];
              pp.ocurve.alpha0[i]  = pp.curve.alpha0[mod(j,m)];
              pp.ocurve.beta[i]    = pp.curve.beta[mod(j,m)];
              s[i] = t[i] = 1.0;
            } else {
              pp.ocurve.tag[i] = PotraceCurveTo;
              pp.ocurve.c[i][0] = opt[j].c[0];
              pp.ocurve.c[i][1] = opt[j].c[1];
              pp.ocurve.c[i][2] = pp.curve.c[mod(j,m)][2];
              pp.ocurve.vertex[i] = interval(opt[j].s, pp.curve.c[mod(j,m)][2], pp.curve.vertex[mod(j,m)]);
              pp.ocurve.alpha[i] = opt[j].alpha;
              pp.ocurve.alpha0[i] = opt[j].alpha;
              s[i] = opt[j].s;
              t[i] = opt[j].t;
            }
            j = pt[j];
          }

          /* calculate beta parameters */
          for (i=0; i<om; i++) {
            i1 = mod(i+1,om);
            pp.ocurve.beta[i] = s[i] / (s[i] + t[i1]);
          }
          pp.ocurve.alphacurve = 1;
          
          return 0;
        }

        /* ---------------------------------------------------------------------- */

        /* return 0 on success, 1 on error with errno set. */
        internal static int process_path(path_t plist, PotraceParam param, progress_t progress) {
            double nn = 0, cn = 0;

            if (progress.callback != null) {
                /* precompute task size for progress estimates */
                nn = 0;
                list_forall(plist, p => nn += p.Priv.len);
                cn = 0;
            }
  
            /* call downstream function with each path */
            list_forall (plist, p => {
                calc_sums(p.Priv);
                calc_lon(p.Priv);
                bestpolygon(p.Priv);
                adjust_vertices(p.Priv);
                if (p.Sign == '-') {   /* reverse orientation of negative paths */
                    reverse(p.Priv.curve);
                }
                smooth(p.Priv.curve, param.AlphaMax);
                if (param.OptiCurve) {
                    opticurve(p.Priv, param.OptTolerance);
                    p.Priv.fcurve = p.Priv.ocurve;
                } else {
                    p.Priv.fcurve = p.Priv.curve;
                }
                privcurve_to_curve(p.Priv.fcurve, p.Curve);

                if (progress.callback != null) {
                    cn += p.Priv.len;
                    progress_update(cn/nn, progress);
                }
            });

            progress_update(1.0, progress);

            return 0;
        }
    }
}
