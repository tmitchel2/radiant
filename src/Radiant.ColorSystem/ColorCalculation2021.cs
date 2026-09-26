// Copyright 2025 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

namespace Radiant.ColorSystem;

/// <summary>Color calculation for the 2021 spec.</summary>
internal sealed class ColorCalculation2021 : ColorCalculation
{
    public override Hct GetHct(DynamicScheme scheme, DynamicColor color)
    {
        var tone = color.GetTone(scheme);
        var palette = color.Palette(scheme);
        return palette.GetHct(tone);
    }

    public override double GetTone(DynamicScheme scheme, DynamicColor color)
    {
        var decreasingContrast = scheme.ContrastLevel < 0;
        var toneDeltaPair = color.ToneDeltaPair?.Invoke(scheme);

        // Case 1: dual foreground, pair of colors with delta constraint.
        if (toneDeltaPair is not null)
        {
            var roleA = toneDeltaPair.RoleA;
            var roleB = toneDeltaPair.RoleB;
            var delta = toneDeltaPair.Delta;
            var polarity = toneDeltaPair.Polarity;
            var stayTogether = toneDeltaPair.StayTogether;

            var aIsNearer = polarity == TonePolarity.Nearer
                || (polarity == TonePolarity.Lighter && !scheme.IsDark)
                || (polarity == TonePolarity.Darker && scheme.IsDark);
            var nearer = aIsNearer ? roleA : roleB;
            var farther = aIsNearer ? roleB : roleA;
            var amNearer = color.Name == nearer.Name;
            var expansionDir = scheme.IsDark ? 1 : -1;
            var nTone = nearer.Tone(scheme);
            var fTone = farther.Tone(scheme);

            // 1st round: solve to min for each, if background and contrast curve are defined.
            if (color.Background is not null && nearer.ContrastCurve is not null && farther.ContrastCurve is not null)
            {
                var bg = color.Background(scheme);
                var nContrastCurve = nearer.ContrastCurve(scheme);
                var fContrastCurve = farther.ContrastCurve(scheme);
                if (bg is not null && nContrastCurve is not null && fContrastCurve is not null)
                {
                    var bgTone = bg.GetTone(scheme);
                    var nContrast = nContrastCurve.Get(scheme.ContrastLevel);
                    var fContrast = fContrastCurve.Get(scheme.ContrastLevel);
                    // If a color is good enough, it is not adjusted.
                    // Initial and adjusted tones for `nearer`
                    if (Contrast.RatioOfTones(bgTone, nTone) < nContrast)
                    {
                        nTone = DynamicColor.ForegroundTone(bgTone, nContrast);
                    }
                    // Initial and adjusted tones for `farther`
                    if (Contrast.RatioOfTones(bgTone, fTone) < fContrast)
                    {
                        fTone = DynamicColor.ForegroundTone(bgTone, fContrast);
                    }
                    if (decreasingContrast)
                    {
                        // If decreasing contrast, adjust color to the "bare minimum" that satisfies contrast.
                        nTone = DynamicColor.ForegroundTone(bgTone, nContrast);
                        fTone = DynamicColor.ForegroundTone(bgTone, fContrast);
                    }
                }
            }

            if ((fTone - nTone) * expansionDir < delta)
            {
                // 2nd round: expand farther to match delta, if contrast is not satisfied.
                fTone = MathUtils.ClampDouble(0, 100, nTone + delta * expansionDir);
                if ((fTone - nTone) * expansionDir >= delta)
                {
                    // Good! Tones now satisfy the constraint; no change needed.
                }
                else
                {
                    // 3rd round: contract nearer to match delta.
                    nTone = MathUtils.ClampDouble(0, 100, fTone - delta * expansionDir);
                }
            }

            // Avoids the 50-59 awkward zone.
            if (50 <= nTone && nTone < 60)
            {
                // If `nearer` is in the awkward zone, move it away, together with `farther`.
                if (expansionDir > 0)
                {
                    nTone = 60;
                    fTone = System.Math.Max(fTone, nTone + delta * expansionDir);
                }
                else
                {
                    nTone = 49;
                    fTone = System.Math.Min(fTone, nTone + delta * expansionDir);
                }
            }
            else if (50 <= fTone && fTone < 60)
            {
                if (stayTogether)
                {
                    // Fixes both, to avoid two colors on opposite sides of the "awkward zone".
                    if (expansionDir > 0)
                    {
                        nTone = 60;
                        fTone = System.Math.Max(fTone, nTone + delta * expansionDir);
                    }
                    else
                    {
                        nTone = 49;
                        fTone = System.Math.Min(fTone, nTone + delta * expansionDir);
                    }
                }
                else
                {
                    // Not required to stay together; fixes just one.
                    fTone = expansionDir > 0 ? 60 : 49;
                }
            }

            // Returns `nTone` if this color is `nearer`, otherwise `fTone`.
            return amNearer ? nTone : fTone;
        }
        else
        {
            // Case 2: No contrast pair; just solve for itself.
            var answer = color.Tone(scheme);

            if (!HasResolvedBackground(scheme, color))
            {
                return answer; // No adjustment for colors with no background.
            }

            var bgTone = color.Background!(scheme)!.GetTone(scheme);
            var desiredRatio = color.ContrastCurve!(scheme)!.Get(scheme.ContrastLevel);

            if (Contrast.RatioOfTones(bgTone, answer) >= desiredRatio)
            {
                // Don't "improve" what's good enough.
            }
            else
            {
                // Rough improvement.
                answer = DynamicColor.ForegroundTone(bgTone, desiredRatio);
            }

            if (decreasingContrast)
            {
                answer = DynamicColor.ForegroundTone(bgTone, desiredRatio);
            }

            if (color.IsBackground && 50 <= answer && answer < 60)
            {
                // Must adjust
                answer = Contrast.RatioOfTones(49, bgTone) >= desiredRatio ? 49 : 60;
            }

            if (!HasResolvedSecondBackground(scheme, color))
            {
                return answer;
            }

            // Case 3: Adjust for dual backgrounds.
            return AdjustForDualBackgrounds(scheme, color, answer, desiredRatio);
        }
    }
}
