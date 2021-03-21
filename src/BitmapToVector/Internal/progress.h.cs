/* Copyright (C) 2001-2019 Peter Selinger.
   Copyright (C) 2021 Dalton Spillman.
   This file is part of a C# port of Potrace. It is free software and it is covered
   by the GNU General Public License. See README.md and LICENSE for details. */

/* operations on potrace_progress_t objects, which are defined in
   potracelib.h. Note: the code attempts to minimize runtime overhead
   when no progress monitoring was requested. It also tries to
   minimize excessive progress calculations beneath the "epsilon"
   threshold. */

using System.Runtime.CompilerServices;
using progress_t = BitmapToVector.Internal.PotraceInternal.progress_s;

namespace BitmapToVector.Internal
{
    public partial class PotraceInternal
    {
        /* structure to hold progress bar callback data */
        internal class progress_s {
            public PotraceProgress.ProgressBarCallbackDelegate callback; /* callback fn */
            public object data;          /* callback function's private data */
            public double min, max;      /* desired range of progress, e.g. 0.0 to 1.0 */
            public double epsilon;       /* granularity: can skip smaller increments */
            public double b;             /* upper limit of subrange in superrange units */
            public double d_prev;        /* previous value of d */
        };

        /* notify given progress object of current progress. Note that d is
           given in the 0.0-1.0 range, which will be scaled and translated to
           the progress object's range. */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void progress_update(double d, progress_t prog) {
            double d_scaled;

            if (prog != null && prog.callback != null) {
                d_scaled = prog.min * (1-d) + prog.max * d;
                if (d == 1.0 || d_scaled >= prog.d_prev + prog.epsilon) {
                    prog.callback(prog.min * (1-d) + prog.max * d, prog.data);
                    prog.d_prev = d_scaled;
                }
            }
        }

        /* start a subrange of the given progress object. The range is
           narrowed to [a..b], relative to 0.0-1.0 coordinates. If new range
           is below granularity threshold, disable further subdivisions. */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void progress_subrange_start(double a, double b, progress_t prog, progress_t sub) {
            double min, max;

            if (prog == null || prog.callback == null) {
                sub.callback = null;
                return;
            }

            min = prog.min * (1-a) + prog.max * a;
            max = prog.min * (1-b) + prog.max * b;

            if (max - min < prog.epsilon) {
                sub.callback = null;    /* no further progress info in subrange */
                sub.b = b;
                return;
            }
            sub.callback = prog.callback;
            sub.data = prog.data;
            sub.epsilon = prog.epsilon;
            sub.min = min;
            sub.max = max;
            sub.d_prev = prog.d_prev;
            return;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void progress_subrange_end(progress_t prog, progress_t sub) {
            if (prog != null && prog.callback != null) {
                if (sub.callback == null) {
                    progress_update(sub.b, prog);
                } else {
                    prog.d_prev = sub.d_prev;
                }
            }    
        }
    }
}
