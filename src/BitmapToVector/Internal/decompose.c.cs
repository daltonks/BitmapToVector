/* Copyright (C) 2001-2019 Peter Selinger.
   Copyright (C) 2021 Dalton Spillman.
   This file is part of a C# port of Potrace. It is free software and it is covered
   by the GNU General Public License. See README.md and LICENSE for details. */

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static BitmapToVector.PotraceParam;
using bbox_t = BitmapToVector.Internal.PotraceInternal.bbox_s;
using path_t = BitmapToVector.PotracePath;
using point_t = BitmapToVector.Internal.PotraceInternal.point_s;
using progress_t = BitmapToVector.Internal.PotraceInternal.progress_s;

namespace BitmapToVector.Internal
{
    public unsafe partial class PotraceInternal
    {
        /* ---------------------------------------------------------------------- */
        /* deterministically and efficiently hash (x,y) into a pseudo-random bit */

        static readonly byte[] detrand_t = { 
            /* non-linear sequence: constant term of inverse in GF(8), 
               mod x^8+x^4+x^3+x+1 */
            0, 1, 1, 0, 1, 0, 1, 1, 0, 1, 1, 0, 0, 1, 1, 1, 0, 0, 0, 1, 1, 1, 0, 1, 
            0, 1, 1, 0, 1, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 1, 1, 0, 0, 1, 0, 0, 0, 0, 
            0, 1, 0, 0, 1, 1, 0, 0, 0, 1, 0, 1, 1, 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 1, 
            1, 0, 1, 1, 0, 1, 1, 1, 1, 0, 1, 0, 0, 0, 1, 1, 0, 0, 0, 0, 1, 0, 1, 1, 
            0, 0, 1, 1, 1, 0, 0, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 0, 0, 
            0, 0, 0, 0, 1, 0, 1, 0, 1, 0, 1, 0, 0, 1, 0, 0, 1, 0, 1, 1, 1, 0, 1, 0, 
            0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 1, 0, 1, 0, 0, 1, 1, 0, 1, 0, 
            0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 
            1, 0, 1, 1, 0, 0, 0, 1, 1, 1, 1, 0, 1, 0, 0, 0, 0, 1, 0, 1, 1, 1, 0, 0, 
            0, 1, 0, 1, 1, 0, 0, 1, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 1, 1, 0, 0, 1, 
            1, 1, 0, 0, 0, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int detrand(int x, int y) {
            uint z;
            
            /* 0x04b3e375 and 0x05a8ef93 are chosen to contain every possible
               5-bit sequence */
            z = (uint) ((((uint)0x04b3e375 * x) ^ y) * 0x05a8ef93);
            z = (uint) (detrand_t[z & 0xff] ^ detrand_t[(z>>8) & 0xff] ^ detrand_t[(z>>16) & 0xff] ^ detrand_t[(z>>24) & 0xff]);
            return (int) z;
        }

        /* ---------------------------------------------------------------------- */
        /* auxiliary bitmap manipulations */

        /* set the excess padding to 0 */
        static void bm_clearexcess(PotraceBitmap bm) {
            ulong mask;
            int y;

            if (bm.W % BM_WORDBITS != 0) {
                mask = BM_ALLBITS << (BM_WORDBITS - (bm.W % BM_WORDBITS));
                for (y=0; y<bm.H; y++) {
                    *bm_index(bm, bm.W, y) &= mask;
                }
            }
        }

        internal class bbox_s {
            public int x0, x1, y0, y1;    /* bounding box */
        };

        /* clear the bm, assuming the bounding box is set correctly (faster
            than clearing the whole bitmap) */
        static void clear_bm_with_bbox(PotraceBitmap bm, bbox_t bbox) {
            int imin = (bbox.x0 / BM_WORDBITS);
            int imax = ((bbox.x1 + BM_WORDBITS-1) / BM_WORDBITS);
            int i, y;

            for (y=bbox.y0; y<bbox.y1; y++) {
                for (i=imin; i<imax; i++) {
                    bm_scanline(bm, y)[i] = 0;
                }
            }
        }

        /* ---------------------------------------------------------------------- */
        /* auxiliary functions */

        /* return the "majority" value of bitmap bm at intersection (x,y). We
           assume that the bitmap is balanced at "radius" 1.  */
        static bool majority(PotraceBitmap bm, int x, int y) {
            int i, a, ct;

            for (i=2; i<5; i++) { /* check at "radius" i */
                ct = 0;
                for (a=-i+1; a<=i-1; a++) {
                    ct += BM_GET(bm, x+a, y+i-1) ? 1 : -1;
                    ct += BM_GET(bm, x+i-1, y+a-1) ? 1 : -1;
                    ct += BM_GET(bm, x+a-1, y-i) ? 1 : -1;
                    ct += BM_GET(bm, x-i, y+a) ? 1 : -1;
                }
                if (ct>0) {
                    return true;
                } else if (ct<0) {
                    return false;
                }
            }
            return false;
        }

        /* ---------------------------------------------------------------------- */
        /* decompose image into paths */

        /* efficiently invert bits [x,infty) and [xa,infty) in line y. Here xa
           must be a multiple of BM_WORDBITS. */
        static void xor_to_ref(PotraceBitmap bm, int x, int y, int xa) {
            int xhi = x & -BM_WORDBITS;
            int xlo = x & (BM_WORDBITS-1);  /* = x % BM_WORDBITS */
            int i;
  
            if (xhi<xa) {
                for (i = xhi; i < xa; i+=BM_WORDBITS) {
                    *bm_index(bm, i, y) ^= BM_ALLBITS;
                }
            } else {
                for (i = xa; i < xhi; i+=BM_WORDBITS) {
                    *bm_index(bm, i, y) ^= BM_ALLBITS;
                }
            }
            /* note: the following "if" is needed because x86 treats a<<b as
               a<<(b&31). I spent hours looking for this bug. */
            if (xlo != 0) {
                *bm_index(bm, xhi, y) ^= (BM_ALLBITS << (BM_WORDBITS - xlo));
            }
        }

        /* a path is represented as an array of points, which are thought to
           lie on the corners of pixels (not on their centers). The path point
           (x,y) is the lower left corner of the pixel (x,y). Paths are
           represented by the len/pt components of a path_t object (which
           also stores other information about the path) */

        /* xor the given pixmap with the interior of the given path. Note: the
           path must be within the dimensions of the pixmap. */
        static void xor_path(PotraceBitmap bm, path_t p) {
            int xa, x, y, k, y1;

            if (p.Priv.len <= 0) {  /* a path of length 0 is silly, but legal */
                return;
            }

            y1 = (int) p.Priv.pt[p.Priv.len-1].y;

            xa = (int) (p.Priv.pt[0].x & -BM_WORDBITS);
            for (k=0; k<p.Priv.len; k++) {
                x = (int) p.Priv.pt[k].x;
                y = (int) p.Priv.pt[k].y;

                if (y != y1) {
                    /* efficiently invert the rectangle [x,xa] x [y,y1] */
                    xor_to_ref(bm, x, min(y,y1), xa);
                    y1 = y;
                }
            }
        }

        /* Find the bounding box of a given path. Path is assumed to be of
           non-zero length. */
        static void setbbox_path(bbox_t bbox, path_t p) {
            int x, y;
            int k;

            bbox.y0 = int.MaxValue;
            bbox.y1 = 0;
            bbox.x0 = int.MaxValue;
            bbox.x1 = 0;

            for (k=0; k<p.Priv.len; k++) {
                x = (int) p.Priv.pt[k].x;
                y = (int) p.Priv.pt[k].y;

                if (x < bbox.x0) {
                    bbox.x0 = x;
                }
                if (x > bbox.x1) {
                    bbox.x1 = x;
                }
                if (y < bbox.y0) {
                    bbox.y0 = y;
                }
                if (y > bbox.y1) {
                    bbox.y1 = y;
                }
            }
        }

        /* compute a path in the given pixmap, separating black from white.
           Start path at the point (x0,x1), which must be an upper left corner
           of the path. Also compute the area enclosed by the path. Return a
           new path_t object, or NULL on error (note that a legitimate path
           cannot have length 0). Sign is required for correct interpretation
           of turnpolicies. */
        static path_t findpath(PotraceBitmap bm, int x0, int y0, int sign, int turnpolicy) {
          int x, y, dirx, diry;
          long area;
          bool c, d;
          int tmp;
          point_t pt;
          var points = new List<point_t>();
          path_t p = null;

          x = x0;
          y = y0;
          dirx = 0;
          diry = -1;
          
          pt = null;
          area = 0;
          
          while (true) {
            /* add point to path */
            pt = new point_t();
            points.Add(pt);
            pt.x = x;
            pt.y = y;
            
            /* move to next point */
            x += dirx;
            y += diry;
            area += x*diry;

            /* path complete? */
            if (x==x0 && y==y0) {
              break;
            }
            
            /* determine next direction */
            c = BM_GET(bm, x + (dirx+diry-1)/2, y + (diry-dirx-1)/2);
            d = BM_GET(bm, x + (dirx-diry-1)/2, y + (diry+dirx-1)/2);
            
            if (c && !d) {               /* ambiguous turn */
              if (turnpolicy == PotraceTurnpolicyRight
	          || (turnpolicy == PotraceTurnPolicyBlack && sign == '+')
	          || (turnpolicy == PotraceTurnPolicyWhite && sign == '-')
	          || (turnpolicy == PotraceTurnpolicyRandom && detrand(x,y) != 0)
	          || (turnpolicy == PotraceTurnpolicyMajority && majority(bm, x, y))
	          || (turnpolicy == PotraceTurnpolicyMinority && !majority(bm, x, y))) {
	        tmp = dirx;              /* right turn */
	        dirx = diry;
	        diry = -tmp;
              } else {
	        tmp = dirx;              /* left turn */
	        dirx = -diry;
	        diry = tmp;
              }
            } else if (c) {              /* right turn */
              tmp = dirx;
              dirx = diry;
              diry = -tmp;
            } else if (!d) {             /* left turn */
              tmp = dirx;
              dirx = -diry;
              diry = tmp;
            }
          } /* while this path */

          /* allocate new path object */
          p = path_new();

          p.Priv.pt = points;
          p.Priv.len = points.Count;
          p.Area = area <= int.MaxValue ? (int) area : int.MaxValue; /* avoid overflow */
          p.Sign = sign;

          return p;
        }

        /* Give a tree structure to the given path list, based on "insideness"
           testing. I.e., path A is considered "below" path B if it is inside
           path B. The input pathlist is assumed to be ordered so that "outer"
           paths occur before "inner" paths. The tree structure is stored in
           the "childlist" and "sibling" components of the path_t
           structure. The linked list structure is also changed so that
           negative path components are listed immediately after their
           positive parent.  Note: some backends may ignore the tree
           structure, others may use it e.g. to group path components. We
           assume that in the input, point 0 of each path is an "upper left"
           corner of the path, as returned by bm_to_pathlist. This makes it
           easy to find an "interior" point. The bm argument should be a
           bitmap of the correct size (large enough to hold all the paths),
           and will be used as scratch space. Return 0 on success or -1 on
           error with errno set. */

        static void pathlist_to_tree(path_t plist, PotraceBitmap bm) {
            path_t p = null, p1;
            path_t heap, heap1;
            path_t cur;
            path_t head;
            var plist_hook = new LambdaProperty<path_t>();          /* for fast appending to linked list */
            var hook_in = new LambdaProperty<path_t>();
            var hook_out = new LambdaProperty<path_t>(); /* for fast appending to linked list */
            bbox_t bbox = new bbox_s();
            
            bm_clear(bm);

            /* save original "next" pointers */
            list_forall(plist, path => {
                path.Sibling = path.Next;
                path.ChildList = null;
            });
          
            heap = plist;

            /* the heap holds a list of lists of paths. Use "childlist" field
               for outer list, "next" field for inner list. Each of the sublists
               is to be turned into a tree. This code is messy, but it is
               actually fast. Each path is rendered exactly once. We use the
               heap to get a tail recursive algorithm: the heap holds a list of
               pathlists which still need to be transformed. */

            while (heap != null) {
                /* unlink first sublist */
                cur = heap;
                heap = heap.ChildList;
                cur.ChildList = null;
                
                /* unlink first path */
                head = cur;
                cur = cur.Next;
                head.Next = null;
                
                /* render path */
                xor_path(bm, head);
                setbbox_path(bbox, head);
                
                /* now do insideness test for each element of cur; append it to
                   head.childlist if it's inside head, else append it to
                   head.next. */
                hook_in.Change(() => head.ChildList, path => head.ChildList = path);
                hook_out.Change(() => head.Next, path => head.Next = path);
                  
                for (p=cur; p != null; p=cur) {
                    cur = p.Next;
                    p.Next = null;
                    if (p.Priv.pt[0].y <= bbox.y0) {
                        list_insert_beforehook(p, hook_out);
	                    /* append the remainder of the list to hook_out */
                        hook_out.Change(() => cur, path => cur = path);
	                    break;
                    }
                    if (BM_GET(bm, (int) p.Priv.pt[0].x, (int) (p.Priv.pt[0].y-1))) {
                        list_insert_beforehook(p, hook_in);
                    } else {
                        list_insert_beforehook(p, hook_out);
                    }
                }
                
                /* clear bm */
                clear_bm_with_bbox(bm, bbox);
                
                /* now schedule head.childlist and head.next for further
                   processing */
                if (head.Next != null) {
                    head.Next.ChildList = heap;
                    heap = head.Next;
                }
                if (head.ChildList != null) {
                    head.ChildList.ChildList = heap;
                    heap = head.ChildList;
                }
            }
            
            /* copy sibling structure from "next" to "sibling" component */
            p = plist;
            while (p != null) {
                p1 = p.Sibling;
                p.Sibling = p.Next;
                p = p1;
            }

            /* reconstruct a new linked list ("next") structure from tree
               ("childlist", "sibling") structure. This code is slightly messy,
               because we use a heap to make it tail recursive: the heap
               contains a list of childlists which still need to be
               processed. */
            heap = plist;
            if (heap != null) {
                heap.Next = null;  /* heap is a linked list of childlists */
            }
            plist = null;
            plist_hook.Change(() => plist, path => plist = path);
            while (heap != null) {
                heap1 = heap.Next;
                for (p=heap; p != null; p=p.Sibling) {
                    /* p is a positive path */
                    /* append to linked list */
                    list_insert_beforehook(p, plist_hook);
                    
                    /* go through its children */
                    for (p1=p.ChildList; p1 != null; p1=p1.Sibling) {
	                    /* append to linked list */
                        list_insert_beforehook(p1, plist_hook);
	                    /* append its childlist to heap, if non-empty */
	                    if (p1.ChildList != null) {
	                        var _hook = new LambdaProperty<path_t>();
                            _hook.Change(() => heap1, path => heap1 = path);

                            void MoveHookToNext()
                            {
                                var path = _hook.Value;
                                _hook.Change(() => path.Next, v => path.Next = v);
                            }
		                    for (; _hook.Value!=null; MoveHookToNext()) {}
		                    p1.ChildList.Next = _hook.Value; _hook.Value = p1.ChildList;
	                    }
                    }
                }
                heap = heap1;
            }

            return;
        }

        /* find the next set pixel in a row <= y. Pixels are searched first
           left-to-right, then top-down. In other words, (x,y)<(x',y') if y>y'
           or y=y' and x<x'. If found, return 0 and store pixel in
           (*xp,*yp). Else return 1. Note that this function assumes that
           excess bytes have been cleared with bm_clearexcess. */
        static int findnext(PotraceBitmap bm, int *xp, int *yp) {
            int x;
            int y;
            int x0;

            x0 = (*xp) & ~(BM_WORDBITS-1);

            for (y=*yp; y>=0; y--) {
                for (x=x0; x<bm.W && x>=0; x+=BM_WORDBITS) {
                    if (*bm_index(bm, x, y) != 0) {
                        while (!BM_GET(bm, x, y)) {
                            x++;
                        }
                        /* found */
                        *xp = x;
                        *yp = y;
                        return 0;
                    }
                }
                x0 = 0;
            }
            /* not found */
            return 1;
        }

        internal static int bm_to_pathlist(PotraceBitmap bm, out path_t plistp, PotraceParam param, progress_t progress) {
            int x;
            int y;
            path_t p;
            path_t plist = null;  /* linked list of path objects */
            var plist_hook = new LambdaProperty<path_t>();  /* used to speed up appending to linked list */
            plist_hook.Change(() => plist, value => plist = value);
            PotraceBitmap bm1 = null;
            int sign;

            bm1 = bm_dup(bm);

            /* be sure the byte padding on the right is set to 0, as the fast
               pixel search below relies on it */
            bm_clearexcess(bm1);

            /* iterate through components */
            x = 0;
            y = bm1.H - 1;
            while (findnext(bm1, &x, &y) == 0) { 
                /* calculate the sign by looking at the original */
                sign = BM_GET(bm, x, y) ? '+' : '-';

                /* calculate the path */
                p = findpath(bm1, x, y+1, sign, param.TurnPolicy);

                /* update buffered image */
                xor_path(bm1, p);

                /* if it's a turd, eliminate it, else append it to the list */
                if (p.Area <= param.TurdSize) {
                    path_free(p);
                } else {
                    list_insert_beforehook(p, plist_hook);
                }

                if (bm1.H > 0) { /* to be sure */
                    progress_update(1-y/(double)bm1.H, progress);
                }
            }

            pathlist_to_tree(plist, bm1);
            bm_free(bm1);
            plistp = plist;

            progress_update(1.0, progress);

            return 0;
        }
    }
}
