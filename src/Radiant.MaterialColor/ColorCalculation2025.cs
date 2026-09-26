// Copyright 2025 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

using System;

namespace Radiant.MaterialColor;

/// <summary>Color calculation for the 2025 spec and later.</summary>
internal sealed class ColorCalculation2025 : ColorCalculation
{
    public override Hct GetHct(DynamicScheme scheme, DynamicColor color)
    {
        var palette = color.Palette(scheme);
        var tone = color.GetTone(scheme);
        var multiplier = color.ChromaMultiplier is not null ? color.ChromaMultiplier(scheme) : 1;
        if (multiplier == 1)
        {
            return palette.GetHct(tone);
        }

        var chroma = palette.Chroma * multiplier;
        if (tone == 99 && Hct.IsYellow(palette.Hue))
        {
            return TonalPalette.FromHueAndChroma(palette.Hue, chroma).GetHct(tone);
        }
        return Hct.From(palette.Hue, chroma, tone);
    }

    public override double GetTone(DynamicScheme scheme, DynamicColor color)
    {
        var toneDeltaPair = color.ToneDeltaPair?.Invoke(scheme);

        // Case 0: tone delta constraint.
        if (toneDeltaPair is not null)
        {
            var roleA = toneDeltaPair.RoleA;
            var roleB = toneDeltaPair.RoleB;
            var polarity = toneDeltaPair.Polarity;
            var constraint = toneDeltaPair.Constraint;
            var absoluteDelta = polarity == TonePolarity.Darker
                || (polarity == TonePolarity.RelativeLighter && scheme.IsDark)
                || (polarity == TonePolarity.RelativeDarker && !scheme.IsDark)
                ? -toneDeltaPair.Delta
                : toneDeltaPair.Delta;

            var amRoleA = color.Name == roleA.Name;
            var selfRole = amRoleA ? roleA : roleB;
            var refRole = amRoleA ? roleB : roleA;
            var selfTone = selfRole.Tone(scheme);
            var refTone = refRole.GetTone(scheme);
            var relativeDelta = absoluteDelta * (amRoleA ? 1 : -1);

            if (constraint == DeltaConstraint.Exact)
            {
                selfTone = MathUtils.ClampDouble(0, 100, refTone + relativeDelta);
            }
            else if (constraint == DeltaConstraint.Nearer)
            {
                if (relativeDelta > 0)
                {
                    selfTone = MathUtils.ClampDouble(0, 100, MathUtils.ClampDouble(refTone, refTone + relativeDelta, selfTone));
                }
                else
                {
                    selfTone = MathUtils.ClampDouble(0, 100, MathUtils.ClampDouble(refTone + relativeDelta, refTone, selfTone));
                }
            }
            else if (constraint == DeltaConstraint.Farther)
            {
                if (relativeDelta > 0)
                {
                    selfTone = MathUtils.ClampDouble(refTone + relativeDelta, 100, selfTone);
                }
                else
                {
                    selfTone = MathUtils.ClampDouble(0, refTone + relativeDelta, selfTone);
                }
            }

            if (color.Background is not null && color.ContrastCurve is not null)
            {
                var background = color.Background(scheme);
                var contrastCurve = color.ContrastCurve(scheme);
                if (background is not null && contrastCurve is not null)
                {
                    // Adjust the tones for contrast, if background and contrast curve are defined.
                    var bgTone = background.GetTone(scheme);
                    var selfContrast = contrastCurve.Get(scheme.ContrastLevel);
                    selfTone = Contrast.RatioOfTones(bgTone, selfTone) >= selfContrast && scheme.ContrastLevel >= 0
                        ? selfTone
                        : DynamicColor.ForegroundTone(bgTone, selfContrast);
                }
            }

            // This can avoid the awkward tones for background colors including the access fixed
            // colors. Accent fixed dim colors should not be adjusted.
            if (color.IsBackground && !color.Name.EndsWith("_fixed_dim", StringComparison.Ordinal))
            {
                selfTone = selfTone >= 57
                    ? MathUtils.ClampDouble(65, 100, selfTone)
                    : MathUtils.ClampDouble(0, 49, selfTone);
            }

            return selfTone;
        }
        else
        {
            // Case 1: No tone delta pair; just solve for itself.
            var answer = color.Tone(scheme);

            if (!HasResolvedBackground(scheme, color))
            {
                return answer; // No adjustment for colors with no background.
            }

            var bgTone = color.Background!(scheme)!.GetTone(scheme);
            var desiredRatio = color.ContrastCurve!(scheme)!.Get(scheme.ContrastLevel);

            // Recalculate the tone from desired contrast ratio if the current contrast ratio is not
            // enough or desired contrast level is decreasing (<0).
            answer = Contrast.RatioOfTones(bgTone, answer) >= desiredRatio && scheme.ContrastLevel >= 0
                ? answer
                : DynamicColor.ForegroundTone(bgTone, desiredRatio);

            // This can avoid the awkward tones for background colors including the access fixed
            // colors. Accent fixed dim colors should not be adjusted.
            if (color.IsBackground && !color.Name.EndsWith("_fixed_dim", StringComparison.Ordinal))
            {
                answer = answer >= 57
                    ? MathUtils.ClampDouble(65, 100, answer)
                    : MathUtils.ClampDouble(0, 49, answer);
            }

            if (!HasResolvedSecondBackground(scheme, color))
            {
                return answer;
            }

            // Case 2: Adjust for dual backgrounds.
            return AdjustForDualBackgrounds(scheme, color, answer, desiredRatio);
        }
    }
}
