﻿/* Copyright (C) 2001-2019 Peter Selinger.
   Copyright (C) 2021 Dalton Spillman.
   This file is part of a C# port of Potrace. It is free software and it is covered
   by the GNU General Public License. See README.md and LICENSE for details. */

namespace BitmapToVector.Internal
{
    public partial class PotraceInternal
    {
        /* an interval [min, max] */
        internal class interval_s {
            public double min, max;
        };
    }
}
