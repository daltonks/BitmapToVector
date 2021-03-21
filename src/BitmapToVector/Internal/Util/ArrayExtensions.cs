/* Copyright (C) 2001-2019 Peter Selinger.
   Copyright (C) 2021 Dalton Spillman.
   This file is part of a C# port of Potrace. It is free software and it is covered
   by the GNU General Public License. See README.md and LICENSE for details. */

using System;

namespace BitmapToVector.Internal.Util
{
    static class ArrayExtensions
    {
        public static T[] SetAll<T>(this T[] array, Func<T> create)
        {
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = create();
            }
            return array;
        }
    }
}
