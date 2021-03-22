/* Copyright (C) 2001-2019 Peter Selinger.
   Copyright (C) 2021 Dalton Spillman.
   This file is part of a C# port of Potrace. It is free software and it is covered
   by the GNU General Public License. See README.md and LICENSE for details. */

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BitmapToVector.Internal;

namespace BitmapToVector
{
    /* ---------------------------------------------------------------------- */
    /* tracing parameters */
    
    /* structure to hold progress bar callback data */
    public class PotraceProgress {
        public delegate void ProgressBarCallbackDelegate(double progress, object privdata);

        public ProgressBarCallbackDelegate Callback; /* callback fn */
        public object Data;     /* callback function's private data */
        public double Min, Max; /* desired range of progress, e.g. 0.0 to 1.0 */
        public double Epsilon;  /* granularity: can skip smaller increments */
    };

    /* structure to hold tracing parameters */
    public class PotraceParam {
        /* turn policies */
        public const int PotraceTurnPolicyBlack = 0;
        public const int PotraceTurnPolicyWhite = 1;
        public const int PotraceTurnPolicyLeft = 2;
        public const int PotraceTurnpolicyRight = 3;
        public const int PotraceTurnpolicyMinority = 4;
        public const int PotraceTurnpolicyMajority = 5;
        public const int PotraceTurnpolicyRandom = 6;

        public int TurdSize = 2;        /* area of largest path to be ignored */
        public int TurnPolicy = PotraceTurnpolicyMinority;      /* resolves ambiguous turns in path decomposition */
        public double AlphaMax = 1.0;     /* corner threshold */
        public bool OptiCurve = true;       /* use curve optimization? */
        public double OptTolerance = 0.2; /* curve optimization tolerance */
        public PotraceProgress Progress = new PotraceProgress { /* progress callback function */
            Callback = null,      /* callback function */
            Data = null,          /* callback data */
            Min = 0.0, Max = 1.0, /* progress range */
            Epsilon = 0.0         /* granularity */
        };
    };

    /* ---------------------------------------------------------------------- */
    /* bitmaps */
    
    /* Internal bitmap format. The n-th scanline starts at scanline(n) =
       (map + n*dy). Raster data is stored as a sequence of ulongs
       (NOT bytes). The leftmost bit of scanline n is the most significant
       bit of scanline(n)[0]. */
    public unsafe class PotraceBitmap : IDisposable {
        /// <summary>
        /// Creates an all-white bitmap.
        /// </summary>
        public static PotraceBitmap Create(int w, int h)
        {
            return PotraceInternal.bm_new(w, h);
        }

        public int W { get; internal set; }    /* width and height, in pixels */
        public int H { get; internal set; }
        internal int Dy { get; set; }          /* words per scanline (not bytes) */
        internal ulong* Map { get; private set; }  /* raw data, dy*h words */

        ulong[] _mapArray;
        public ulong[] MapArray
        {
            get => _mapArray;
            internal set
            {
                _mapArray = value;
                _mapGcHandle?.Free();
                _mapGcHandle = GCHandle.Alloc(MapArray, GCHandleType.Pinned);
                Map = (ulong*) _mapGcHandle.Value.AddrOfPinnedObject().ToPointer();
            }
        }
        
        GCHandle? _mapGcHandle;

        internal PotraceBitmap() { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetBlackUnsafe(int x, int y)
        {
            PotraceInternal.BM_USET(this, x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetWhiteUnsafe(int x, int y)
        {
            PotraceInternal.BM_UCLR(this, x, y);
        }

        public void Dispose()
        {
            _mapGcHandle?.Free();
            _mapGcHandle = null;
        }

        ~PotraceBitmap()
        {
            Dispose();
        }
    };

    /* ---------------------------------------------------------------------- */
    /* curves */

    /* point */
    public class PotraceDPoint {
        public double X, Y;

        public PotraceDPoint Clone()
        {
            return new PotraceDPoint { X = X, Y = Y };
        }
    };

    /* closed curve segment */
    public class PotraceCurve {
        /* segment tags */
        public const int PotraceCurveTo = 1;
        public const int PotraceCorner = 2;

        public int N;                    /* number of segments */
        public int[] Tag;                 /* tag[n]: POTRACE_CURVETO or POTRACE_CORNER */
        public PotraceDPoint[][] C; /* c[n][3]: control points. 
			   c[n][0] is unused for tag[n]=POTRACE_CORNER */
    };

    /* Linked list of signed curve segments. Also carries a tree structure. */
    public class PotracePath {
        public int Area;                         /* area of the bitmap path */
        public char Sign;                         /* '+' or '-', depending on orientation */
        public PotraceCurve Curve = new PotraceCurve();            /* this path's vector data */

        public PotracePath Next;      /* linked list structure */

        public PotracePath ChildList; /* tree structure */
        public PotracePath Sibling;   /* tree structure */

        internal PotraceInternal.potrace_privpath_s Priv;  /* private state */
    };

    /* ---------------------------------------------------------------------- */
    /* Potrace state */
    
    public class PotraceState {
       public const int PotraceStatusOk = 0;
       public const int PotraceStatusIncomplete = 1;

       // In C#, this is always PotraceStatusOk, so keep it internal for now
       internal int Status;
       public PotracePath Plist;            /* vector data */

       internal object Priv; /* private state */
    }
}
