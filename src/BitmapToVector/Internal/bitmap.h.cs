/* Copyright (C) 2001-2019 Peter Selinger.
   Copyright (C) 2021 Dalton Spillman.
   This file is part of a C# port of Potrace. It is free software and it is covered
   by the GNU General Public License. See README.md and LICENSE for details. */

using System;
using System.Runtime.CompilerServices;

namespace BitmapToVector.Internal
{
    public unsafe partial class PotraceInternal
    {
        /* The present file defines some convenient macros and static inline
           functions for accessing bitmaps. Since they only produce inline
           code, they can be conveniently shared by the library and frontends,
           if desired */

        /* ---------------------------------------------------------------------- */
        /* some measurements */

        const int BM_WORDSIZE = (sizeof(ulong));
        const int BM_WORDBITS = (8*BM_WORDSIZE);
        const ulong BM_HIBIT = (((ulong)1)<<(BM_WORDBITS-1));
        const ulong BM_ALLBITS = (~(ulong) 0);
        
        /* macros for accessing pixel at index (x,y). U* macros omit the
           bounds check. */
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong* bm_scanline(PotraceBitmap bm, int y) {
            return (bm.Map + (long)y*bm.Dy);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong* bm_index(PotraceBitmap bm, int x, int y)
        {
            return bm_scanline(bm, y) + x / BM_WORDBITS;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong bm_mask(int x) => (BM_HIBIT >> ((x) & (BM_WORDBITS-1)));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool bm_range(int x, int a) => ((int)(x) >= 0 && (int)(x) < (a));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool bm_safe(PotraceBitmap bm, int x, int y) => (bm_range(x, (bm).W) && bm_range(y, (bm).H));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool BM_UGET(PotraceBitmap bm, int x, int y) => ((*bm_index(bm, x, y) & bm_mask(x)) != 0);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong BM_USET(PotraceBitmap bm, int x, int y) => (*bm_index(bm, x, y) |= bm_mask(x));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong BM_UCLR(PotraceBitmap bm, int x, int y) => (*bm_index(bm, x, y) &= ~bm_mask(x));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong BM_UINV(PotraceBitmap bm, int x, int y) => (*bm_index(bm, x, y) ^= bm_mask(x));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong BM_UPUT(PotraceBitmap bm, int x, int y, bool b) => ((b) ? BM_USET(bm, x, y) : BM_UCLR(bm, x, y));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool BM_GET(PotraceBitmap bm, int x, int y) => (bm_safe(bm, x, y) ? BM_UGET(bm, x, y) : false);
        internal static ulong BM_SET(PotraceBitmap bm, int x, int y) => (bm_safe(bm, x, y) ? BM_USET(bm, x, y) : 0);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong BM_CLR(PotraceBitmap bm, int x, int y) => (bm_safe(bm, x, y) ? BM_UCLR(bm, x, y) : 0);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong BM_INV(PotraceBitmap bm, int x, int y) => (bm_safe(bm, x, y) ? BM_UINV(bm, x, y) : 0);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong BM_PUT(PotraceBitmap bm, int x, int y, bool b) => (bm_safe(bm, x, y) ? BM_UPUT(bm, x, y, b) : 0);

        /* calculate the size, in bytes, required for the data area of a
           bitmap of the given dy and h. Assume h >= 0. Return -1 if the size
           does not fit into the ptrdiff_t type. */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long getsize(int dy, int h) {
            long size;

            if (dy < 0) {
                dy = -dy;
            }
  
            size = (long)dy * (long)h * (long)BM_WORDSIZE;

            /* check for overflow error */
            if (size < 0 || (h != 0 && dy != 0 && size / h / dy != BM_WORDSIZE)) {
                throw new Exception("Bitmap size out of bounds");
            }

            return size;
        }

        /* return the size, in bytes, of the data area of the bitmap. Return
           -1 if the size does not fit into the ptrdiff_t type; however, this
           cannot happen if the bitmap is well-formed, i.e., if created with
           bm_new or bm_dup. */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static long bm_size(PotraceBitmap bm) {
            return getsize(bm.Dy, bm.H);
        }

        /* calculate the base address of the bitmap data. Assume that the
           bitmap is well-formed, i.e., its size fits into the ptrdiff_t type.
           This is the case if created with bm_new or bm_dup. The base address
           may differ from bm->map if dy is negative */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong* bm_base(PotraceBitmap bm) {
            int dy = bm.Dy;

            if (dy >= 0 || bm.H == 0) {
                return bm.Map;
            } else {
                return bm_scanline(bm, bm.H - 1);
            }  
        }

        /* free the given bitmap. Leaves errno untouched. */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void bm_free(PotraceBitmap bm) {
            bm.Dispose();
        }

        /* return new bitmap initialized to 0. NULL with errno on error.
            Assumes w, h >= 0. */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static PotraceBitmap bm_new(int w, int h) {
            PotraceBitmap bm;
            int dy = w == 0 ? 0 : (w - 1) / BM_WORDBITS + 1;
            long size;

            size = getsize(dy, h);
            if (size == 0) {
                size = BM_WORDSIZE; /* make sure calloc() doesn't return NULL */
            } 

            bm = new PotraceBitmap();
            bm.W = w;
            bm.H = h;
            bm.Dy = dy;
            bm.MapArray = new ulong[size];
            return bm;
        }

        /* clear the given bitmap. Set all bits to c. Assumes a well-formed
           bitmap. */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void bm_clear(PotraceBitmap bm) {
            /* Note: if the bitmap was created with bm_new, then it is
               guaranteed that size will fit into the ptrdiff_t type. */
            long size = bm_size(bm);
            memset(bm_base(bm), 0, size);
        }
        
        /* duplicate the given bitmap. Return NULL on error with errno
           set. Assumes a well-formed bitmap. */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static PotraceBitmap bm_dup(PotraceBitmap bm) {
            PotraceBitmap bm1 = bm_new(bm.W, bm.H);
            Array.Copy(bm.MapArray, bm1.MapArray, bm.MapArray.Length);
            return bm1;
        }
    }
}
