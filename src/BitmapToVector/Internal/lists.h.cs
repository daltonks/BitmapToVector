/* Copyright (C) 2001-2019 Peter Selinger.
   Copyright (C) 2021 Dalton Spillman.
   This file is part of a C# port of Potrace. It is free software and it is covered
   by the GNU General Public License. See README.md and LICENSE for details. */

using System;
using path_t = BitmapToVector.PotracePath;

namespace BitmapToVector.Internal
{
    // TODO: Complete the C# port of this file
    public partial class PotraceInternal
    {
        static void list_forall(path_t path, Action<path_t> action)
        {
            var current = path;
            while (current != null)
            {
                action(current);
                current = current.Next;
            }
        }

        static void list_insert_beforehook(path_t path, LambdaProperty<path_t> hook)
        {
            path.Next = hook.Value;
            hook.Value = path;
            hook.Change(() => path.Next, p => path.Next = p);
        }
    }
}
