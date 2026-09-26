// Ported from msdfgen (https://github.com/Chlumsky/msdfgen) at commit 1c106ed8, core/EdgeColor.h.
// Copyright (c) 2014 - 2025 Viktor Chlumsky, licensed under the MIT License (see THIRD-PARTY-NOTICES.md).

using System;

namespace Radiant.Text.Msdf;

/// <summary>
/// Which of the three channels an edge contributes its distance to. Edges that meet at a corner
/// share at most one channel, so at the corner the median of the channels keeps the corner sharp
/// instead of rounding it as a single distance would.
/// </summary>
[Flags]
internal enum EdgeColor
{
    Black = 0,
    Red = 1,
    Green = 2,
    Yellow = 3,
    Blue = 4,
    Magenta = 5,
    Cyan = 6,
    White = 7,
}
