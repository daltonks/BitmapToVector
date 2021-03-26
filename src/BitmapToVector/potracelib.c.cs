/* Copyright (C) 2001-2019 Peter Selinger.
   Copyright (C) 2021 Dalton Spillman.
   This file is part of a C# port of Potrace. It is free software and it is covered
   by the GNU General Public License. See README.md and LICENSE for details. */

using System;
using System.Diagnostics;
using BitmapToVector.Internal;
using static BitmapToVector.PotraceState;
using progress_t = BitmapToVector.Internal.PotraceInternal.progress_s;

namespace BitmapToVector
{
    public class Potrace
    {
        public static PotraceState Trace(PotraceParam param, PotraceBitmap bm) {
            PotracePath plist = null;
            PotraceState st;
            progress_t prog = new progress_t();
            progress_t subprog = new progress_t();
  
            /* prepare private progress bar state */
            prog.callback = param.Progress.Callback;
            prog.data = param.Progress.Data;
            prog.min = param.Progress.Min;
            prog.max = param.Progress.Max;
            prog.epsilon = param.Progress.Epsilon;
            prog.d_prev = param.Progress.Min;

            /* allocate state object */
            st = new PotraceState();
            
            PotraceInternal.progress_subrange_start(0.0, 0.1, prog, subprog);

            /* process the image */
            PotraceInternal.bm_to_pathlist(bm, out plist, param, subprog);

            st.Status = PotraceStatusOk;
            st.Plist = plist;
            st.Priv = null;  /* private state currently unused */

            PotraceInternal.progress_subrange_end(prog, subprog);

            PotraceInternal.progress_subrange_start(0.1, 1.0, prog, subprog);

            /* partial success. */
            PotraceInternal.process_path(plist, param, subprog);

            PotraceInternal.progress_subrange_end(prog, subprog);

            // Quantization
            if (param.QuantizeUnit != null)
            {
                var quantizeUnit = param.QuantizeUnit.Value;
                for (var path = plist; path != null; path = path.Next)
                {
                    var c = path.Curve.C;
                    foreach (var array in c)
                    foreach (var point in array)
                    {
                        point.X = Math.Floor(point.X * quantizeUnit) / quantizeUnit;
                        point.Y = Math.Floor(point.Y * quantizeUnit) / quantizeUnit;
                    }
                }
            }

            return st;
        }
    }
}
