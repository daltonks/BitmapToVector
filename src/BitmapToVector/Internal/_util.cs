/* Copyright (C) 2001-2019 Peter Selinger.
   Copyright (C) 2021 Dalton Spillman.
   This file is part of a C# port of Potrace. It is free software and it is covered
   by the GNU General Public License. See README.md and LICENSE for details. */

using System;

namespace BitmapToVector.Internal
{
    unsafe partial class PotraceInternal
    {
        static void memset(ulong* ptr, ulong value, long size)
        {
            for(var i = 0L; i < size; i++)
            {
                *(ptr + i) = value;
            }
        }

        class LambdaProperty<TValue>
        {
            public Func<TValue> Get { get; set; }
            public Action<TValue> Set { get; set; }

            public TValue Value
            {
                get => Get();
                set => Set(value);
            }

            public void Change(Func<TValue> get, Action<TValue> set)
            {
                Get = get;
                Set = set;
            }
        }
    }
}
