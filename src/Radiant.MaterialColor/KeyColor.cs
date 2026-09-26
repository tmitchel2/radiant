// Copyright 2021 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

using System;
using System.Collections.Generic;

namespace Radiant.MaterialColor;

/// <summary>
/// Key color is a color that represents the hue and chroma of a tonal palette. Used once per
/// search, so its cache needs no locking.
/// </summary>
internal sealed class KeyColor
{
    private const double MaxChromaValue = 200.0;

    // Cache that maps tone to max chroma to avoid duplicated HCT calculation.
    private readonly Dictionary<int, double> _chromaCache = [];
    private readonly double _hue;
    private readonly double _requestedChroma;

    public KeyColor(double hue, double requestedChroma)
    {
        _hue = hue;
        _requestedChroma = requestedChroma;
    }

    /// <summary>
    /// Creates a key color from a hue and a chroma. The key color is the first tone, starting from
    /// T50, matching the given hue and chroma.
    /// </summary>
    public Hct Create()
    {
        // Pivot around T50 because T50 has the most chroma available, on
        // average. Thus it is most likely to have a direct answer.
        const int pivotTone = 50;
        const int toneStepSize = 1;
        // Epsilon to accept values slightly higher than the requested chroma.
        const double epsilon = 0.01;

        // Binary search to find the tone that can provide a chroma that is closest
        // to the requested chroma.
        var lowerTone = 0;
        var upperTone = 100;
        while (lowerTone < upperTone)
        {
            var midTone = (lowerTone + upperTone) / 2;
            var isAscending = MaxChroma(midTone) < MaxChroma(midTone + toneStepSize);
            var sufficientChroma = MaxChroma(midTone) >= _requestedChroma - epsilon;

            if (sufficientChroma)
            {
                // Either range [lowerTone, midTone] or [midTone, upperTone] has
                // the answer, so search in the range that is closer the pivot tone.
                if (Math.Abs(lowerTone - pivotTone) < Math.Abs(upperTone - pivotTone))
                {
                    upperTone = midTone;
                }
                else
                {
                    if (lowerTone == midTone)
                    {
                        return Hct.From(_hue, _requestedChroma, lowerTone);
                    }
                    lowerTone = midTone;
                }
            }
            else
            {
                // As there is no sufficient chroma in the midTone, follow the direction
                // to the chroma peak.
                if (isAscending)
                {
                    lowerTone = midTone + toneStepSize;
                }
                else
                {
                    // Keep midTone for potential chroma peak.
                    upperTone = midTone;
                }
            }
        }

        return Hct.From(_hue, _requestedChroma, lowerTone);
    }

    // Find the maximum chroma for a given tone
    private double MaxChroma(int tone)
    {
        if (_chromaCache.TryGetValue(tone, out var cached))
        {
            return cached;
        }
        var chroma = Hct.From(_hue, MaxChromaValue, tone).Chroma;
        _chromaCache[tone] = chroma;
        return chroma;
    }
}
