/* Copyright (C) 2001-2019 Peter Selinger.
   Copyright (C) 2021 Dalton Spillman.
   This file is part of a C# port of Potrace. It is free software and it is covered
   by the GNU General Public License. See README.md and LICENSE for details. */

/* vertex is c[1] for tag=POTRACE_CORNER, and the intersection of
   .c[-1][2]..c[0] and c[1]..c[2] for tag=POTRACE_CURVETO. alpha is only
   defined for tag=POTRACE_CURVETO and is the alpha parameter of the curve:
   .c[-1][2]..c[0] = alpha*(.c[-1][2]..vertex), and
   c[2]..c[1] = alpha*(c[2]..vertex).
   Beta is so that (.beta[i])[.vertex[i],.vertex[i+1]] = .c[i][2].
*/

using System.Collections.Generic;
using dpoint_t = BitmapToVector.PotraceDPoint;
using privcurve_t = BitmapToVector.Internal.PotraceInternal.privcurve_s;
using sums_t = BitmapToVector.Internal.PotraceInternal.sums_s;
using point_t = BitmapToVector.Internal.PotraceInternal.point_s;

namespace BitmapToVector.Internal
{
    public partial class PotraceInternal
    {
        internal class privcurve_s {
            public int n;            /* number of segments */
            public int[] tag;         /* tag[n]: POTRACE_CORNER or POTRACE_CURVETO */
            // TODO: Unsure about this one
            public dpoint_t[][] c; /* c[n][i]: control points. 
		       c[n][0] is unused for tag[n]=POTRACE_CORNER */
            /* the remainder of this structure is special to privcurve, and is
               used in EPS debug output and special EPS "short coding". These
               fields are valid only if "alphacurve" is set. */
            public int alphacurve;   /* have the following fields been initialized? */
            public dpoint_t[] vertex; /* for POTRACE_CORNER, this equals c[1] */
            public double[] alpha;    /* only for POTRACE_CURVETO */
            public double[] alpha0;   /* "uncropped" alpha parameter - for debug output only */
            public double[] beta;
        };

        internal class sums_s {
            public double x;
            public double y;
            public double x2;
            public double xy;
            public double y2;
        };

        /* the path structure is filled in with information about a given path
           as it is accumulated and passed through the different stages of the
           Potrace algorithm. Backends only need to read the fcurve and fm
           fields of this data structure, but debugging backends may read
           other fields. */
        internal class potrace_privpath_s {
            public int len;
            public List<point_t> pt;     /* pt[len]: path as extracted from bitmap */
            public int[] lon;        /* lon[len]: (i,lon[i]) = longest straight line from i */

            public int x0, y0;      /* origin for sums */
            public sums_t[] sums;    /* sums[len+1]: cache for fast summing */

            public int m;           /* length of optimal polygon */
            public int[] po;         /* po[m]: optimal polygon */

            public privcurve_t curve = new privcurve_t();   /* curve[m]: array of curve elements */
            public privcurve_t ocurve = new privcurve_t();  /* ocurve[om]: array of curve elements */
            public privcurve_t fcurve = new privcurve_t();  /* final curve: this points to either curve or
		       ocurve. Do not free this separately. */
        };
    }
}
