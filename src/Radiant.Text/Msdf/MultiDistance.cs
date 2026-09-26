// Ported from msdfgen (https://github.com/Chlumsky/msdfgen) at commit 1c106ed8, core/edge-selectors.h.
// Copyright (c) 2014 - 2025 Viktor Chlumsky, licensed under the MIT License (see THIRD-PARTY-NOTICES.md).

using System;

namespace Radiant.Text.Msdf;

/// <summary>A distance per channel, in shape units: positive inside.</summary>
internal readonly record struct MultiDistance(double R, double G, double B)
{
    /// <summary>The median of the three, which is the distance the field reconstructs.</summary>
    public double Median => Math.Max(Math.Min(R, G), Math.Min(Math.Max(R, G), B));
}
