/* Copyright (C) 2001-2019 Peter Selinger.
   Copyright (C) 2021 Dalton Spillman.
   This file is part of a C# port of Potrace. It is free software and it is covered
   by the GNU General Public License. See README.md and LICENSE for details. */

using BitmapToVector.Internal.Util;
using privpath_t = BitmapToVector.Internal.PotraceInternal.potrace_privpath_s;
using path_t = BitmapToVector.PotracePath;
using dpoint_t = BitmapToVector.PotraceDPoint;
using privcurve_t = BitmapToVector.Internal.PotraceInternal.privcurve_s;
using potrace_curve_t = BitmapToVector.PotraceCurve;

namespace BitmapToVector.Internal
{
    public partial class PotraceInternal
    {
        /* ---------------------------------------------------------------------- */
        /* allocate and free path objects */

        static path_t path_new()
        {
            path_t p = null;
            privpath_t priv = null;

            p = new path_t();
            priv = new privpath_t();
            p.Priv = priv;
            return p;
        }

        /* free the members of the given curve structure. Leave errno unchanged. */
        static void privcurve_free_members(privcurve_t curve) {
            // (no-op in C#)
        }

        /* free a path. Leave errno untouched. */
        static void path_free(path_t p) {
            // (no-op in C#)
        }  

        /* free a pathlist, leaving errno untouched. */
        void pathlist_free(path_t plist) {
            // (no-op in C#)
        }

        /* ---------------------------------------------------------------------- */
        /* initialize and finalize curve structures */
        
        /* initialize the members of the given curve structure to size m.
           Return 0 on success, 1 on error with errno set. */
        static int privcurve_init(privcurve_t curve, int n) {
            curve.n = n;
            curve.tag = new int[n];
            curve.c = new dpoint_t[n][].SetAll(() => new dpoint_t[3].SetAll(() => new dpoint_t()));
            curve.vertex = new dpoint_t[n].SetAll(() => new dpoint_t());
            curve.alpha = new double[n];
            curve.alpha0 = new double[n];
            curve.beta = new double[n];
            return 0;
        }

        /* copy private to public curve structure */
        static void privcurve_to_curve(privcurve_t pc, potrace_curve_t c) {
            c.N = pc.n;
            c.Tag = pc.tag;
            c.C = pc.c;
        }
    }
}
