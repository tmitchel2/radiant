// Copyright 2025 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md), translated mechanically from its
// TypeScript and verified against its outputs; the structure deliberately mirrors upstream's so a
// newer upstream can be diffed and ported the same way.

using System.Linq;

namespace Radiant.ColorSystem;

/// <summary>The Radiant color roles as the 2026 spec defines them. Upstream's <c>ColorSpecDelegateImpl2026</c>.</summary>
internal sealed class ColorSpec2026 : ColorSpec2025
{
    private static double TMaxC(TonalPalette palette, double lowerBound = 0, double upperBound = 100, double chromaMultiplier = 1)
    {
        var answer = FindBestToneForChroma(
            palette.Hue, palette.Chroma * chromaMultiplier, 100, true);
        return MathUtils.ClampDouble(lowerBound, upperBound, answer);
    }

    private static double TMinC(TonalPalette palette, double lowerBound = 0, double upperBound = 100)
    {
        var answer = FindBestToneForChroma(palette.Hue, palette.Chroma, 0, false);
        return MathUtils.ClampDouble(lowerBound, upperBound, answer);
    }

    private static double FindBestToneForChroma(double hue, double chroma, double tone, bool byDecreasingTone)
    {
        var answer = tone;
        var bestCandidate = Hct.From(hue, chroma, answer);
        while (bestCandidate.Chroma < chroma)
        {
            if (tone < 0 || tone > 100)
            {
                break;
            }
            tone += byDecreasingTone ? -1.0 : 1.0;
            var newCandidate = Hct.From(hue, chroma, tone);
            if (bestCandidate.Chroma < newCandidate.Chroma)
            {
                bestCandidate = newCandidate;
                answer = tone;
            }
        }

        return answer;
    }

    private static ContrastCurve GetCurve(double defaultContrast)
    {
        if (defaultContrast == 1.5)
        {
            return new ContrastCurve(1.5, 1.5, 3, 5.5);
        }
        else if (defaultContrast == 3)
        {
            return new ContrastCurve(3, 3, 4.5, 7);
        }
        else if (defaultContrast == 4.5)
        {
            return new ContrastCurve(4.5, 4.5, 7, 11);
        }
        else if (defaultContrast == 6)
        {
            return new ContrastCurve(6, 6, 7, 11);
        }
        else if (defaultContrast == 7)
        {
            return new ContrastCurve(7, 7, 11, 21);
        }
        else if (defaultContrast == 9)
        {
            return new ContrastCurve(9, 9, 11, 21);
        }
        else if (defaultContrast == 11)
        {
            return new ContrastCurve(11, 11, 21, 21);
        }
        else if (defaultContrast == 21)
        {
            return new ContrastCurve(21, 21, 21, 21);
        }
        else
        {
            // Shouldn't happen.
            return new ContrastCurve(defaultContrast, defaultContrast, 7, 21);
        }
    }


    public override DynamicColor Surface()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "surface",
          palette: (s) => s.NeutralPalette,
          tone: (s) =>
          {
              if (s.Variant == Variant.Cmf)
              {
                  return s.IsDark ? 4 : 98;
              }
              else
              {  // Undefined use case
                  return 0;
              }
          },
          isBackground: true
    );
        return DynamicColor.ExtendSpecVersion(base.Surface(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor SurfaceDim()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "surface_dim",
          palette: (s) => s.NeutralPalette,
          tone: (s) =>
          {
              if (s.Variant == Variant.Cmf)
              {
                  return s.IsDark ? 4 : 87;
              }
              else
              {  // Undefined use case
                  return 0;
              }
          },
          chromaMultiplier: (s) =>
          {
              if (s.Variant == Variant.Cmf)
              {
                  return s.IsDark ? 1 : 1.7;
              }
              else
              {  // Undefined use case
                  return 0;
              }
          },
          isBackground: true
    );
        return DynamicColor.ExtendSpecVersion(base.SurfaceDim(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor SurfaceBright()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "surface_bright",
          palette: (s) => s.NeutralPalette,
          tone: (s) =>
          {
              if (s.Variant == Variant.Cmf)
              {
                  return s.IsDark ? 18 : 98;
              }
              else
              {  // Undefined use case
                  return 0;
              }
          },
          chromaMultiplier: (s) =>
          {
              if (s.Variant == Variant.Cmf)
              {
                  return s.IsDark ? 1.7 : 1;
              }
              else
              {  // Undefined use case
                  return 0;
              }
          },
          isBackground: true
    );
        return DynamicColor.ExtendSpecVersion(base.SurfaceBright(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor SurfaceContainerLowest()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "surface_container_lowest",
          palette: (s) => s.NeutralPalette,
          tone: (s) =>
          {
              if (s.Variant == Variant.Cmf)
              {
                  return s.IsDark ? 0 : 100;
              }
              else
              {  // Undefined use case
                  return 0;
              }
          },
          isBackground: true
    );
        return DynamicColor.ExtendSpecVersion(base.SurfaceContainerLowest(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor SurfaceContainerLow()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "surface_container_low",
          palette: (s) => s.NeutralPalette,
          tone: (s) =>
          {
              if (s.Variant == Variant.Cmf)
              {
                  return s.IsDark ? 6 : 96;
              }
              else
              {  // Undefined use case
                  return 0;
              }
          },
          chromaMultiplier: (s) =>
          {
              if (s.Variant == Variant.Cmf)
              {
                  return 1.25;
              }
              else
              {  // Undefined use case
                  return 0;
              }
          },
          isBackground: true
    );
        return DynamicColor.ExtendSpecVersion(base.SurfaceContainerLow(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor SurfaceContainer()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "surface_container",
          palette: (s) => s.NeutralPalette,
          tone: (s) =>
          {
              if (s.Variant == Variant.Cmf)
              {
                  return s.IsDark ? 9 : 94;
              }
              else
              {  // Undefined use case
                  return 0;
              }
          },
          chromaMultiplier: (s) =>
          {
              if (s.Variant == Variant.Cmf)
              {
                  return 1.4;
              }
              else
              {  // Undefined use case
                  return 0;
              }
          },
          isBackground: true
    );
        return DynamicColor.ExtendSpecVersion(base.SurfaceContainer(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor SurfaceContainerHigh()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "surface_container_high",
          palette: (s) => s.NeutralPalette,
          tone: (s) =>
          {
              if (s.Variant == Variant.Cmf)
              {
                  return s.IsDark ? 12 : 92;
              }
              else
              {  // Undefined use case
                  return 0;
              }
          },
          chromaMultiplier: (s) =>
          {
              if (s.Variant == Variant.Cmf)
              {
                  return 1.5;
              }
              else
              {  // Undefined use case
                  return 0;
              }
          },
          isBackground: true
    );
        return DynamicColor.ExtendSpecVersion(base.SurfaceContainerHigh(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor SurfaceContainerHighest()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "surface_container_highest",
          palette: (s) => s.NeutralPalette,
          tone: (s) =>
          {
              if (s.Variant == Variant.Cmf)
              {
                  return s.IsDark ? 15 : 90;
              }
              else
              {  // Undefined use case
                  return 0;
              }
          },
          chromaMultiplier: (s) =>
          {
              if (s.Variant == Variant.Cmf)
              {
                  return 1.7;
              }
              else
              {  // Undefined use case
                  return 0;
              }
          },
          isBackground: true
    );
        return DynamicColor.ExtendSpecVersion(
            base.SurfaceContainerHighest(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor OnSurface()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "on_surface",
          palette: (s) => s.NeutralPalette,
          chromaMultiplier: (s) =>
          {
              if (s.Variant == Variant.Cmf)
              {
                  return 1.7;
              }
              else
              {  // Undefined use case
                  return 0;
              }
          },
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => s.IsDark ? GetCurve(11) : GetCurve(9)
    );
        return DynamicColor.ExtendSpecVersion(base.OnSurface(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor OnSurfaceVariant()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "on_surface_variant",
          palette: (s) => s.NeutralPalette,
          chromaMultiplier: (s) =>
          {
              if (s.Variant == Variant.Cmf)
              {
                  return 1.7;
              }
              else
              {  // Undefined variant
                  return 0;
              }
          },
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => s.IsDark ? GetCurve(6) : GetCurve(4.5)
    );
        return DynamicColor.ExtendSpecVersion(base.OnSurfaceVariant(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor Outline()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "outline",
          palette: (s) => s.NeutralPalette,
          chromaMultiplier: (s) =>
          {
              if (s.Variant == Variant.Cmf)
              {
                  return 1.7;
              }
              else
              {  // Undefined use case
                  return 0;
              }
          },
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => GetCurve(3)
    );
        return DynamicColor.ExtendSpecVersion(base.Outline(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor OutlineVariant()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "outline_variant",
          palette: (s) => s.NeutralPalette,
          chromaMultiplier: (s) =>
          {
              if (s.Variant == Variant.Cmf)
              {
                  return 1.7;
              }
              else
              {  // Undefined use case
                  return 0;
              }
          },
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => GetCurve(1.5)
    );
        return DynamicColor.ExtendSpecVersion(base.OutlineVariant(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor InverseSurface()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "inverse_surface",
          palette: (s) => s.NeutralPalette,
          tone: (s) =>
          {
              return s.IsDark ? 98 : 4;
          },
          chromaMultiplier: (s) =>
          {
              if (s.Variant == Variant.Cmf)
              {
                  return 1.7;
              }
              else
              {  // Undefined use case
                  return 0;
              }
          },
          isBackground: true
    );
        return DynamicColor.ExtendSpecVersion(base.InverseSurface(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor InverseOnSurface()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "inverse_on_surface",
          palette: (s) => s.NeutralPalette,
          background: (s) => InverseSurface(),
          contrastCurve: (s) => GetCurve(7)
    );
        return DynamicColor.ExtendSpecVersion(base.InverseOnSurface(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor Primary()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "primary",
          palette: (s) => s.PrimaryPalette,
          tone: (s) => s.SourceColorHct.Chroma <= 12 ? (s.IsDark ? 80 : 40) :
                                                       s.SourceColorHct.Tone,
          isBackground: true,
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => GetCurve(4.5),
          toneDeltaPair: (s) => s.Platform == Platform.Phone ?
              new ToneDeltaPair(
                  PrimaryContainer(), Primary(), 5, TonePolarity.RelativeLighter,
                  true, DeltaConstraint.Farther) :
              null
    );
        return DynamicColor.ExtendSpecVersion(base.Primary(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor OnPrimary()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "on_primary",
          palette: (s) => s.PrimaryPalette,
          background: (s) => Primary(),
          contrastCurve: (s) => GetCurve(6)
    );
        return DynamicColor.ExtendSpecVersion(base.OnPrimary(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor PrimaryContainer()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "primary_container",
          palette: (s) => s.PrimaryPalette,
          tone: (s) =>
          {
              if (!s.IsDark && s.SourceColorHct.Chroma <= 12)
              {
                  return 90;
              }
              return s.SourceColorHct.Tone > 55 ?
              MathUtils.ClampDouble(61, 90, s.SourceColorHct.Tone) :
              MathUtils.ClampDouble(30, 49, s.SourceColorHct.Tone);
          },
          isBackground: true,
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => s.ContrastLevel > 0 ? GetCurve(1.5) : null
    );
        return DynamicColor.ExtendSpecVersion(base.PrimaryContainer(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor OnPrimaryContainer()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "on_primary_container",
          palette: (s) => s.PrimaryPalette,
          background: (s) => PrimaryContainer(),
          contrastCurve: (s) => GetCurve(6)
    );
        return DynamicColor.ExtendSpecVersion(base.OnPrimaryContainer(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor PrimaryFixed()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "primary_fixed",
          palette: (s) => s.PrimaryPalette,
          tone: (s) =>
          {
              var tempS = s.CloneWith(isDark: false, contrastLevel: 0);
              return PrimaryContainer().GetTone(tempS);
          },
          isBackground: true,
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => s.ContrastLevel > 0 ? GetCurve(1.5) : null
    );
        return DynamicColor.ExtendSpecVersion(base.PrimaryFixed(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor PrimaryFixedDim()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "primary_fixed_dim",
          palette: (s) => s.PrimaryPalette,
          tone: (s) => PrimaryFixed().GetTone(s),
          isBackground: true,
          background: (s) => HighestSurface(s),
          toneDeltaPair: (s) => new ToneDeltaPair(
              PrimaryFixedDim(), PrimaryFixed(), 5, TonePolarity.Darker, true,
              DeltaConstraint.Exact),
          contrastCurve: (s) => s.ContrastLevel > 0 ? GetCurve(1.5) : null
    );
        return DynamicColor.ExtendSpecVersion(base.PrimaryFixedDim(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor OnPrimaryFixed()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "on_primary_fixed",
          palette: (s) => s.PrimaryPalette,
          background: (s) => PrimaryFixed().GetTone(s) > 57 ?
              PrimaryFixedDim() :
              PrimaryFixed(),
          contrastCurve: (s) => GetCurve(7)
    );
        return DynamicColor.ExtendSpecVersion(base.OnPrimaryFixed(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor OnPrimaryFixedVariant()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "on_primary_fixed_variant",
          palette: (s) => s.PrimaryPalette,
          background: (s) => PrimaryFixed().GetTone(s) > 57 ?
              PrimaryFixedDim() :
              PrimaryFixed(),
          contrastCurve: (s) => GetCurve(4.5)
    );
        return DynamicColor.ExtendSpecVersion(base.OnPrimaryFixedVariant(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor InversePrimary()
    {
        return base.InversePrimary();
    }

    public override DynamicColor Secondary()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "secondary",
          palette: (s) => s.SecondaryPalette,
          tone: (s) =>
          {
              return s.IsDark ? TMinC(s.SecondaryPalette) : TMaxC(s.SecondaryPalette);
          },
          isBackground: true,
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => GetCurve(4.5),
          toneDeltaPair: (s) => s.Platform == Platform.Phone ?
              new ToneDeltaPair(
                  SecondaryContainer(), Secondary(), 5,
                  TonePolarity.RelativeLighter, true, DeltaConstraint.Farther) :
              null
    );
        return DynamicColor.ExtendSpecVersion(base.Secondary(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor OnSecondary()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "on_secondary",
          palette: (s) => s.SecondaryPalette,
          background: (s) => Secondary(),
          contrastCurve: (s) => GetCurve(6)
    );
        return DynamicColor.ExtendSpecVersion(base.OnSecondary(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor SecondaryContainer()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "secondary_container",
          palette: (s) => s.SecondaryPalette,
          tone: (s) =>
          {
              return s.IsDark ? TMinC(s.SecondaryPalette, 20, 49) :
                            TMaxC(s.SecondaryPalette, 61, 90);
          },
          isBackground: true,
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => s.ContrastLevel > 0 ? GetCurve(1.5) : null
    );
        return DynamicColor.ExtendSpecVersion(base.SecondaryContainer(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor OnSecondaryContainer()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "on_secondary_container",
          palette: (s) => s.SecondaryPalette,
          background: (s) => SecondaryContainer(),
          contrastCurve: (s) => GetCurve(6)
    );
        return DynamicColor.ExtendSpecVersion(base.OnSecondaryContainer(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor SecondaryFixed()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "secondary_fixed",
          palette: (s) => s.SecondaryPalette,
          tone: (s) =>
          {
              var tempS = s.CloneWith(isDark: false, contrastLevel: 0);
              return SecondaryContainer().GetTone(tempS);
          },
          isBackground: true,
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => s.ContrastLevel > 0 ? GetCurve(1.5) : null
    );
        return DynamicColor.ExtendSpecVersion(base.SecondaryFixed(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor SecondaryFixedDim()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "secondary_fixed_dim",
          palette: (s) => s.SecondaryPalette,
          tone: (s) => SecondaryFixed().GetTone(s),
          isBackground: true,
          background: (s) => HighestSurface(s),
          toneDeltaPair: (s) => new ToneDeltaPair(
              SecondaryFixedDim(), SecondaryFixed(), 5, TonePolarity.Darker, true,
              DeltaConstraint.Exact),
          contrastCurve: (s) => s.ContrastLevel > 0 ? GetCurve(1.5) : null
    );
        return DynamicColor.ExtendSpecVersion(base.SecondaryFixedDim(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor OnSecondaryFixed()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "on_secondary_fixed",
          palette: (s) => s.SecondaryPalette,
          background: (s) => SecondaryFixed().GetTone(s) > 57 ?
              SecondaryFixedDim() :
              SecondaryFixed(),
          contrastCurve: (s) => GetCurve(7)
    );
        return DynamicColor.ExtendSpecVersion(base.OnSecondaryFixed(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor OnSecondaryFixedVariant()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "on_secondary_fixed_variant",
          palette: (s) => s.SecondaryPalette,
          background: (s) => SecondaryFixed().GetTone(s) > 57 ?
              SecondaryFixedDim() :
              SecondaryFixed(),
          contrastCurve: (s) => GetCurve(4.5)
    );
        return DynamicColor.ExtendSpecVersion(
            base.OnSecondaryFixedVariant(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor Tertiary()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "tertiary",
          palette: (s) => s.TertiaryPalette,
          tone: (s) =>
          {
              return s.SourceColorHcts.ElementAtOrDefault(1)?.Tone ?? s.SourceColorHct.Tone;
          },
          isBackground: true,
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => GetCurve(4.5),
          toneDeltaPair: (s) => s.Platform == Platform.Phone ?
              new ToneDeltaPair(
                  TertiaryContainer(), Tertiary(), 5, TonePolarity.RelativeLighter,
                  true, DeltaConstraint.Farther) :
              null
    );
        return DynamicColor.ExtendSpecVersion(base.Tertiary(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor OnTertiary()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "on_tertiary",
          palette: (s) => s.TertiaryPalette,
          background: (s) => Tertiary(),
          contrastCurve: (s) => GetCurve(6)
    );
        return DynamicColor.ExtendSpecVersion(base.OnTertiary(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor TertiaryContainer()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "tertiary_container",
          palette: (s) => s.TertiaryPalette,
          tone: (s) =>
          {
              var secondarySourceColorHct =
              s.SourceColorHcts.ElementAtOrDefault(1) ?? s.SourceColorHct;
              return secondarySourceColorHct.Tone > 55 ?
              MathUtils.ClampDouble(61, 90, secondarySourceColorHct.Tone) :
              MathUtils.ClampDouble(20, 49, secondarySourceColorHct.Tone);
          },
          isBackground: true,
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => s.ContrastLevel > 0 ? GetCurve(1.5) : null
    );
        return DynamicColor.ExtendSpecVersion(base.TertiaryContainer(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor OnTertiaryContainer()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "on_tertiary_container",
          palette: (s) => s.TertiaryPalette,
          background: (s) => TertiaryContainer(),
          contrastCurve: (s) => GetCurve(6)
    );
        return DynamicColor.ExtendSpecVersion(base.OnTertiaryContainer(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor TertiaryFixed()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "tertiary_fixed",
          palette: (s) => s.TertiaryPalette,
          tone: (s) =>
          {
              var tempS = s.CloneWith(isDark: false, contrastLevel: 0);
              return TertiaryContainer().GetTone(tempS);
          },
          isBackground: true,
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => s.ContrastLevel > 0 ? GetCurve(1.5) : null
    );
        return DynamicColor.ExtendSpecVersion(base.TertiaryFixed(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor TertiaryFixedDim()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "tertiary_fixed_dim",
          palette: (s) => s.TertiaryPalette,
          tone: (s) => TertiaryFixed().GetTone(s),
          isBackground: true,
          background: (s) => HighestSurface(s),
          toneDeltaPair: (s) => new ToneDeltaPair(
              TertiaryFixedDim(), TertiaryFixed(), 5, TonePolarity.Darker, true,
              DeltaConstraint.Exact),
          contrastCurve: (s) => s.ContrastLevel > 0 ? GetCurve(1.5) : null
    );
        return DynamicColor.ExtendSpecVersion(base.TertiaryFixedDim(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor OnTertiaryFixed()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "on_tertiary_fixed",
          palette: (s) => s.TertiaryPalette,
          background: (s) => TertiaryFixed().GetTone(s) > 57 ?
              TertiaryFixedDim() :
              TertiaryFixed(),
          contrastCurve: (s) => GetCurve(7)
    );
        return DynamicColor.ExtendSpecVersion(base.OnTertiaryFixed(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor OnTertiaryFixedVariant()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "on_tertiary_fixed_variant",
          palette: (s) => s.TertiaryPalette,
          background: (s) => TertiaryFixed().GetTone(s) > 57 ?
              TertiaryFixedDim() :
              TertiaryFixed(),
          contrastCurve: (s) => GetCurve(4.5)
    );
        return DynamicColor.ExtendSpecVersion(base.OnTertiaryFixedVariant(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor Error()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "error",
          palette: (s) => s.ErrorPalette,
          tone: (s) =>
          {
              return TMaxC(s.ErrorPalette);
          },
          isBackground: true,
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => GetCurve(4.5),
          toneDeltaPair: (s) => s.Platform == Platform.Phone ?
              new ToneDeltaPair(
                  ErrorContainer(), Error(), 5, TonePolarity.RelativeLighter, true,
                  DeltaConstraint.Farther) :
              null
    );
        return DynamicColor.ExtendSpecVersion(base.Error(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor OnError()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "on_error",
          palette: (s) => s.ErrorPalette,
          background: (s) => Error(),
          contrastCurve: (s) => GetCurve(6)
    );
        return DynamicColor.ExtendSpecVersion(base.OnError(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor ErrorContainer()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "error_container",
          palette: (s) => s.ErrorPalette,
          tone: (s) =>
          {
              return s.IsDark ? TMinC(s.ErrorPalette) : TMaxC(s.ErrorPalette);
          },
          isBackground: true,
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => s.ContrastLevel > 0 ? GetCurve(1.5) : null
    );
        return DynamicColor.ExtendSpecVersion(base.ErrorContainer(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor OnErrorContainer()
    {
        var color2026 = DynamicColor.FromPalette(
          name: "on_error_container",
          palette: (s) => s.ErrorPalette,
          background: (s) => ErrorContainer(),
          contrastCurve: (s) => GetCurve(6)
    );
        return DynamicColor.ExtendSpecVersion(base.OnErrorContainer(), SpecVersion.Spec2026, color2026);
    }

    /////////////////////////////////////////////////////////////////
    // Remapped Colors                                             //
    /////////////////////////////////////////////////////////////////

    public override DynamicColor PrimaryDim()
    {
        var color2026 = Primary().With(name: "primary_dim");
        return DynamicColor.ExtendSpecVersion(base.PrimaryDim(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor SecondaryDim()
    {
        var color2026 = Secondary().With(name: "secondary_dim");
        return DynamicColor.ExtendSpecVersion(base.SecondaryDim(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor TertiaryDim()
    {
        var color2026 = Tertiary().With(name: "tertiary_dim");
        return DynamicColor.ExtendSpecVersion(base.TertiaryDim(), SpecVersion.Spec2026, color2026);
    }

    public override DynamicColor ErrorDim()
    {
        var color2026 = Error().With(name: "error_dim");
        return DynamicColor.ExtendSpecVersion(base.ErrorDim(), SpecVersion.Spec2026, color2026);
    }
}
