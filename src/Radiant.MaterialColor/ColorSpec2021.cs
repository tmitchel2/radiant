// Copyright 2025 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md), translated mechanically from its
// TypeScript and verified against its outputs; the structure deliberately mirrors upstream's so a
// newer upstream can be diffed and ported the same way.

using System;

namespace Radiant.MaterialColor;

/// <summary>The Material color roles as the 2021 spec defines them. Upstream's <c>ColorSpecDelegateImpl2021</c>.</summary>
internal class ColorSpec2021 : IColorSpec
{
    private static bool IsFidelity(DynamicScheme scheme)
    {
        return scheme.Variant == Variant.Fidelity ||
            scheme.Variant == Variant.Content;
    }

    private static bool IsMonochrome(DynamicScheme scheme)
    {
        return scheme.Variant == Variant.Monochrome;
    }

    private static double FindDesiredChromaByTone(double hue, double chroma, double tone, bool byDecreasingTone)
    {
        var answer = tone;

        var closestToChroma = Hct.From(hue, chroma, tone);
        if (closestToChroma.Chroma < chroma)
        {
            var chromaPeak = closestToChroma.Chroma;
            while (closestToChroma.Chroma < chroma)
            {
                answer += byDecreasingTone ? -1.0 : 1.0;
                var potentialSolution = Hct.From(hue, chroma, answer);
                if (chromaPeak > potentialSolution.Chroma)
                {
                    break;
                }
                if (Math.Abs(potentialSolution.Chroma - chroma) < 0.4)
                {
                    break;
                }

                var potentialDelta = Math.Abs(potentialSolution.Chroma - chroma);
                var currentDelta = Math.Abs(closestToChroma.Chroma - chroma);
                if (potentialDelta < currentDelta)
                {
                    closestToChroma = potentialSolution;
                }
                chromaPeak = Math.Max(chromaPeak, potentialSolution.Chroma);
            }
        }

        return answer;
    }


    ////////////////////////////////////////////////////////////////
    // Main Palettes                                              //
    ////////////////////////////////////////////////////////////////

    public virtual DynamicColor PrimaryPaletteKeyColor()
    {
        return DynamicColor.FromPalette(
          name: "primary_palette_key_color",
          palette: (s) => s.PrimaryPalette,
          tone: (s) => s.PrimaryPalette.KeyColor.Tone
    );
    }

    public virtual DynamicColor SecondaryPaletteKeyColor()
    {
        return DynamicColor.FromPalette(
          name: "secondary_palette_key_color",
          palette: (s) => s.SecondaryPalette,
          tone: (s) => s.SecondaryPalette.KeyColor.Tone
    );
    }

    public virtual DynamicColor TertiaryPaletteKeyColor()
    {
        return DynamicColor.FromPalette(
          name: "tertiary_palette_key_color",
          palette: (s) => s.TertiaryPalette,
          tone: (s) => s.TertiaryPalette.KeyColor.Tone
    );
    }

    public virtual DynamicColor NeutralPaletteKeyColor()
    {
        return DynamicColor.FromPalette(
          name: "neutral_palette_key_color",
          palette: (s) => s.NeutralPalette,
          tone: (s) => s.NeutralPalette.KeyColor.Tone
    );
    }

    public virtual DynamicColor NeutralVariantPaletteKeyColor()
    {
        return DynamicColor.FromPalette(
          name: "neutral_variant_palette_key_color",
          palette: (s) => s.NeutralVariantPalette,
          tone: (s) => s.NeutralVariantPalette.KeyColor.Tone
    );
    }

    public virtual DynamicColor ErrorPaletteKeyColor()
    {
        return DynamicColor.FromPalette(
          name: "error_palette_key_color",
          palette: (s) => s.ErrorPalette,
          tone: (s) => s.ErrorPalette.KeyColor.Tone
    );
    }

    ////////////////////////////////////////////////////////////////
    // Surfaces [S]                                               //
    ////////////////////////////////////////////////////////////////

    public virtual DynamicColor Background()
    {
        return DynamicColor.FromPalette(
          name: "background",
          palette: (s) => s.NeutralPalette,
          tone: (s) => s.IsDark ? 6 : 98,
          isBackground: true
    );
    }

    public virtual DynamicColor OnBackground()
    {
        return DynamicColor.FromPalette(
          name: "on_background",
          palette: (s) => s.NeutralPalette,
          tone: (s) => s.IsDark ? 90 : 10,
          background: (s) => Background(),
          contrastCurve: (s) => new ContrastCurve(3, 3, 4.5, 7)
    );
    }

    public virtual DynamicColor Surface()
    {
        return DynamicColor.FromPalette(
          name: "surface",
          palette: (s) => s.NeutralPalette,
          tone: (s) => s.IsDark ? 6 : 98,
          isBackground: true
    );
    }

    public virtual DynamicColor SurfaceDim()
    {
        return DynamicColor.FromPalette(
          name: "surface_dim",
          palette: (s) => s.NeutralPalette,
          tone: (s) =>
              s.IsDark ? 6 : new ContrastCurve(87, 87, 80, 75).Get(s.ContrastLevel),
          isBackground: true
    );
    }

    public virtual DynamicColor SurfaceBright()
    {
        return DynamicColor.FromPalette(
          name: "surface_bright",
          palette: (s) => s.NeutralPalette,
          tone: (s) => s.IsDark ?
              new ContrastCurve(24, 24, 29, 34).Get(s.ContrastLevel) :
              98,
          isBackground: true
    );
    }

    public virtual DynamicColor SurfaceContainerLowest()
    {
        return DynamicColor.FromPalette(
          name: "surface_container_lowest",
          palette: (s) => s.NeutralPalette,
          tone: (s) =>
              s.IsDark ? new ContrastCurve(4, 4, 2, 0).Get(s.ContrastLevel) : 100,
          isBackground: true
    );
    }

    public virtual DynamicColor SurfaceContainerLow()
    {
        return DynamicColor.FromPalette(
          name: "surface_container_low",
          palette: (s) => s.NeutralPalette,
          tone: (s) => s.IsDark ?
              new ContrastCurve(10, 10, 11, 12).Get(s.ContrastLevel) :
              new ContrastCurve(96, 96, 96, 95).Get(s.ContrastLevel),
          isBackground: true
    );
    }

    public virtual DynamicColor SurfaceContainer()
    {
        return DynamicColor.FromPalette(
          name: "surface_container",
          palette: (s) => s.NeutralPalette,
          tone: (s) => s.IsDark ?
              new ContrastCurve(12, 12, 16, 20).Get(s.ContrastLevel) :
              new ContrastCurve(94, 94, 92, 90).Get(s.ContrastLevel),
          isBackground: true
    );
    }

    public virtual DynamicColor SurfaceContainerHigh()
    {
        return DynamicColor.FromPalette(
          name: "surface_container_high",
          palette: (s) => s.NeutralPalette,
          tone: (s) => s.IsDark ?
              new ContrastCurve(17, 17, 21, 25).Get(s.ContrastLevel) :
              new ContrastCurve(92, 92, 88, 85).Get(s.ContrastLevel),
          isBackground: true
    );
    }

    public virtual DynamicColor SurfaceContainerHighest()
    {
        return DynamicColor.FromPalette(
          name: "surface_container_highest",
          palette: (s) => s.NeutralPalette,
          tone: (s) => s.IsDark ?
              new ContrastCurve(22, 22, 26, 30).Get(s.ContrastLevel) :
              new ContrastCurve(90, 90, 84, 80).Get(s.ContrastLevel),
          isBackground: true
    );
    }

    public virtual DynamicColor OnSurface()
    {
        return DynamicColor.FromPalette(
          name: "on_surface",
          palette: (s) => s.NeutralPalette,
          tone: (s) => s.IsDark ? 90 : 10,
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => new ContrastCurve(4.5, 7, 11, 21)
    );
    }

    public virtual DynamicColor SurfaceVariant()
    {
        return DynamicColor.FromPalette(
          name: "surface_variant",
          palette: (s) => s.NeutralVariantPalette,
          tone: (s) => s.IsDark ? 30 : 90,
          isBackground: true
    );
    }

    public virtual DynamicColor OnSurfaceVariant()
    {
        return DynamicColor.FromPalette(
          name: "on_surface_variant",
          palette: (s) => s.NeutralVariantPalette,
          tone: (s) => s.IsDark ? 80 : 30,
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => new ContrastCurve(3, 4.5, 7, 11)
    );
    }

    public virtual DynamicColor InverseSurface()
    {
        return DynamicColor.FromPalette(
          name: "inverse_surface",
          palette: (s) => s.NeutralPalette,
          tone: (s) => s.IsDark ? 90 : 20,
          isBackground: true
    );
    }

    public virtual DynamicColor InverseOnSurface()
    {
        return DynamicColor.FromPalette(
          name: "inverse_on_surface",
          palette: (s) => s.NeutralPalette,
          tone: (s) => s.IsDark ? 20 : 95,
          background: (s) => InverseSurface(),
          contrastCurve: (s) => new ContrastCurve(4.5, 7, 11, 21)
    );
    }

    public virtual DynamicColor Outline()
    {
        return DynamicColor.FromPalette(
          name: "outline",
          palette: (s) => s.NeutralVariantPalette,
          tone: (s) => s.IsDark ? 60 : 50,
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => new ContrastCurve(1.5, 3, 4.5, 7)
    );
    }

    public virtual DynamicColor OutlineVariant()
    {
        return DynamicColor.FromPalette(
          name: "outline_variant",
          palette: (s) => s.NeutralVariantPalette,
          tone: (s) => s.IsDark ? 30 : 80,
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => new ContrastCurve(1, 1, 3, 4.5)
    );
    }

    public virtual DynamicColor Shadow()
    {
        return DynamicColor.FromPalette(
          name: "shadow",
          palette: (s) => s.NeutralPalette,
          tone: (s) => 0
    );
    }

    public virtual DynamicColor Scrim()
    {
        return DynamicColor.FromPalette(
          name: "scrim",
          palette: (s) => s.NeutralPalette,
          tone: (s) => 0
    );
    }

    public virtual DynamicColor SurfaceTint()
    {
        return DynamicColor.FromPalette(
          name: "surface_tint",
          palette: (s) => s.PrimaryPalette,
          tone: (s) => s.IsDark ? 80 : 40,
          isBackground: true
    );
    }

    ////////////////////////////////////////////////////////////////
    // Primary [P].                                               //
    ////////////////////////////////////////////////////////////////

    public virtual DynamicColor Primary()
    {
        return DynamicColor.FromPalette(
          name: "primary",
          palette: (s) => s.PrimaryPalette,
          tone: (s) =>
          {
              if (IsMonochrome(s))
              {
                  return s.IsDark ? 100 : 0;
              }
              return s.IsDark ? 80 : 40;
          },
          isBackground: true,
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => new ContrastCurve(3, 4.5, 7, 7),
          toneDeltaPair: (s) => new ToneDeltaPair(
              PrimaryContainer(), Primary(), 10, TonePolarity.Nearer, false)
    );
    }

    public virtual DynamicColor? PrimaryDim()
    {
        return null;
    }

    public virtual DynamicColor OnPrimary()
    {
        return DynamicColor.FromPalette(
          name: "on_primary",
          palette: (s) => s.PrimaryPalette,
          tone: (s) =>
          {
              if (IsMonochrome(s))
              {
                  return s.IsDark ? 10 : 90;
              }
              return s.IsDark ? 20 : 100;
          },
          background: (s) => Primary(),
          contrastCurve: (s) => new ContrastCurve(4.5, 7, 11, 21)
    );
    }

    public virtual DynamicColor PrimaryContainer()
    {
        return DynamicColor.FromPalette(
          name: "primary_container",
          palette: (s) => s.PrimaryPalette,
          tone: (s) =>
          {
              if (IsFidelity(s))
              {
                  return s.SourceColorHct.Tone;
              }
              if (IsMonochrome(s))
              {
                  return s.IsDark ? 85 : 25;
              }
              return s.IsDark ? 30 : 90;
          },
          isBackground: true,
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => new ContrastCurve(1, 1, 3, 4.5),
          toneDeltaPair: (s) => new ToneDeltaPair(
              PrimaryContainer(), Primary(), 10, TonePolarity.Nearer, false)
    );
    }

    public virtual DynamicColor OnPrimaryContainer()
    {
        return DynamicColor.FromPalette(
          name: "on_primary_container",
          palette: (s) => s.PrimaryPalette,
          tone: (s) =>
          {
              if (IsFidelity(s))
              {
                  return DynamicColor.ForegroundTone(
                  PrimaryContainer().Tone(s), 4.5);
              }
              if (IsMonochrome(s))
              {
                  return s.IsDark ? 0 : 100;
              }
              return s.IsDark ? 90 : 30;
          },
          background: (s) => PrimaryContainer(),
          contrastCurve: (s) => new ContrastCurve(3, 4.5, 7, 11)
    );
    }

    public virtual DynamicColor InversePrimary()
    {
        return DynamicColor.FromPalette(
          name: "inverse_primary",
          palette: (s) => s.PrimaryPalette,
          tone: (s) => s.IsDark ? 40 : 80,
          background: (s) => InverseSurface(),
          contrastCurve: (s) => new ContrastCurve(3, 4.5, 7, 7)
    );
    }

    /////////////////////////////////////////////////////////////////
    // Secondary [Q].                                              //
    /////////////////////////////////////////////////////////////////

    public virtual DynamicColor Secondary()
    {
        return DynamicColor.FromPalette(
          name: "secondary",
          palette: (s) => s.SecondaryPalette,
          tone: (s) => s.IsDark ? 80 : 40,
          isBackground: true,
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => new ContrastCurve(3, 4.5, 7, 7),
          toneDeltaPair: (s) => new ToneDeltaPair(
              SecondaryContainer(), Secondary(), 10, TonePolarity.Nearer, false)
    );
    }

    public virtual DynamicColor? SecondaryDim()
    {
        return null;
    }

    public virtual DynamicColor OnSecondary()
    {
        return DynamicColor.FromPalette(
          name: "on_secondary",
          palette: (s) => s.SecondaryPalette,
          tone: (s) =>
          {
              if (IsMonochrome(s))
              {
                  return s.IsDark ? 10 : 100;
              }
              else
              {
                  return s.IsDark ? 20 : 100;
              }
          },
          background: (s) => Secondary(),
          contrastCurve: (s) => new ContrastCurve(4.5, 7, 11, 21)
    );
    }

    public virtual DynamicColor SecondaryContainer()
    {
        return DynamicColor.FromPalette(
          name: "secondary_container",
          palette: (s) => s.SecondaryPalette,
          tone: (s) =>
          {
              var initialTone = s.IsDark ? 30 : 90;
              if (IsMonochrome(s))
              {
                  return s.IsDark ? 30 : 85;
              }
              if (!IsFidelity(s))
              {
                  return initialTone;
              }
              return FindDesiredChromaByTone(
              s.SecondaryPalette.Hue, s.SecondaryPalette.Chroma, initialTone,
              s.IsDark ? false : true);
          },
          isBackground: true,
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => new ContrastCurve(1, 1, 3, 4.5),
          toneDeltaPair: (s) => new ToneDeltaPair(
              SecondaryContainer(), Secondary(), 10, TonePolarity.Nearer, false)
    );
    }

    public virtual DynamicColor OnSecondaryContainer()
    {
        return DynamicColor.FromPalette(
          name: "on_secondary_container",
          palette: (s) => s.SecondaryPalette,
          tone: (s) =>
          {
              if (IsMonochrome(s))
              {
                  return s.IsDark ? 90 : 10;
              }
              if (!IsFidelity(s))
              {
                  return s.IsDark ? 90 : 30;
              }
              return DynamicColor.ForegroundTone(
              SecondaryContainer().Tone(s), 4.5);
          },
          background: (s) => SecondaryContainer(),
          contrastCurve: (s) => new ContrastCurve(3, 4.5, 7, 11)
    );
    }

    /////////////////////////////////////////////////////////////////
    // Tertiary [T].                                               //
    /////////////////////////////////////////////////////////////////

    public virtual DynamicColor Tertiary()
    {
        return DynamicColor.FromPalette(
          name: "tertiary",
          palette: (s) => s.TertiaryPalette,
          tone: (s) =>
          {
              if (IsMonochrome(s))
              {
                  return s.IsDark ? 90 : 25;
              }
              return s.IsDark ? 80 : 40;
          },
          isBackground: true,
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => new ContrastCurve(3, 4.5, 7, 7),
          toneDeltaPair: (s) => new ToneDeltaPair(
              TertiaryContainer(), Tertiary(), 10, TonePolarity.Nearer, false)
    );
    }

    public virtual DynamicColor? TertiaryDim()
    {
        return null;
    }

    public virtual DynamicColor OnTertiary()
    {
        return DynamicColor.FromPalette(
          name: "on_tertiary",
          palette: (s) => s.TertiaryPalette,
          tone: (s) =>
          {
              if (IsMonochrome(s))
              {
                  return s.IsDark ? 10 : 90;
              }
              return s.IsDark ? 20 : 100;
          },
          background: (s) => Tertiary(),
          contrastCurve: (s) => new ContrastCurve(4.5, 7, 11, 21)
    );
    }

    public virtual DynamicColor TertiaryContainer()
    {
        return DynamicColor.FromPalette(
          name: "tertiary_container",
          palette: (s) => s.TertiaryPalette,
          tone: (s) =>
          {
              if (IsMonochrome(s))
              {
                  return s.IsDark ? 60 : 49;
              }
              if (!IsFidelity(s))
              {
                  return s.IsDark ? 30 : 90;
              }
              var proposedHct = s.TertiaryPalette.GetHct(s.SourceColorHct.Tone);
              return DislikeAnalyzer.FixIfDisliked(proposedHct).Tone;
          },
          isBackground: true,
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => new ContrastCurve(1, 1, 3, 4.5),
          toneDeltaPair: (s) => new ToneDeltaPair(
              TertiaryContainer(), Tertiary(), 10, TonePolarity.Nearer, false)
    );
    }

    public virtual DynamicColor OnTertiaryContainer()
    {
        return DynamicColor.FromPalette(
          name: "on_tertiary_container",
          palette: (s) => s.TertiaryPalette,
          tone: (s) =>
          {
              if (IsMonochrome(s))
              {
                  return s.IsDark ? 0 : 100;
              }
              if (!IsFidelity(s))
              {
                  return s.IsDark ? 90 : 30;
              }
              return DynamicColor.ForegroundTone(
              TertiaryContainer().Tone(s), 4.5);
          },
          background: (s) => TertiaryContainer(),
          contrastCurve: (s) => new ContrastCurve(3, 4.5, 7, 11)
    );
    }

    //////////////////////////////////////////////////////////////////
    // Error [E].                                                   //
    //////////////////////////////////////////////////////////////////

    public virtual DynamicColor Error()
    {
        return DynamicColor.FromPalette(
          name: "error",
          palette: (s) => s.ErrorPalette,
          tone: (s) => s.IsDark ? 80 : 40,
          isBackground: true,
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => new ContrastCurve(3, 4.5, 7, 7),
          toneDeltaPair: (s) => new ToneDeltaPair(
              ErrorContainer(), Error(), 10, TonePolarity.Nearer, false)
    );
    }

    public virtual DynamicColor? ErrorDim()
    {
        return null;
    }

    public virtual DynamicColor OnError()
    {
        return DynamicColor.FromPalette(
          name: "on_error",
          palette: (s) => s.ErrorPalette,
          tone: (s) => s.IsDark ? 20 : 100,
          background: (s) => Error(),
          contrastCurve: (s) => new ContrastCurve(4.5, 7, 11, 21)
    );
    }

    public virtual DynamicColor ErrorContainer()
    {
        return DynamicColor.FromPalette(
          name: "error_container",
          palette: (s) => s.ErrorPalette,
          tone: (s) => s.IsDark ? 30 : 90,
          isBackground: true,
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => new ContrastCurve(1, 1, 3, 4.5),
          toneDeltaPair: (s) => new ToneDeltaPair(
              ErrorContainer(), Error(), 10, TonePolarity.Nearer, false)
    );
    }

    public virtual DynamicColor OnErrorContainer()
    {
        return DynamicColor.FromPalette(
          name: "on_error_container",
          palette: (s) => s.ErrorPalette,
          tone: (s) =>
          {
              if (IsMonochrome(s))
              {
                  return s.IsDark ? 90 : 10;
              }
              return s.IsDark ? 90 : 30;
          },
          background: (s) => ErrorContainer(),
          contrastCurve: (s) => new ContrastCurve(3, 4.5, 7, 11)
    );
    }

    //////////////////////////////////////////////////////////////////
    // Primary Fixed [PF]                                           //
    //////////////////////////////////////////////////////////////////

    public virtual DynamicColor PrimaryFixed()
    {
        return DynamicColor.FromPalette(
          name: "primary_fixed",
          palette: (s) => s.PrimaryPalette,
          tone: (s) => IsMonochrome(s) ? 40.0 : 90.0,
          isBackground: true,
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => new ContrastCurve(1, 1, 3, 4.5),
          toneDeltaPair: (s) => new ToneDeltaPair(
              PrimaryFixed(), PrimaryFixedDim(), 10, TonePolarity.Lighter, true)
    );
    }

    public virtual DynamicColor PrimaryFixedDim()
    {
        return DynamicColor.FromPalette(
          name: "primary_fixed_dim",
          palette: (s) => s.PrimaryPalette,
          tone: (s) => IsMonochrome(s) ? 30.0 : 80.0,
          isBackground: true,
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => new ContrastCurve(1, 1, 3, 4.5),
          toneDeltaPair: (s) => new ToneDeltaPair(
              PrimaryFixed(), PrimaryFixedDim(), 10, TonePolarity.Lighter, true)
    );
    }

    public virtual DynamicColor OnPrimaryFixed()
    {
        return DynamicColor.FromPalette(
          name: "on_primary_fixed",
          palette: (s) => s.PrimaryPalette,
          tone: (s) => IsMonochrome(s) ? 100.0 : 10.0,
          background: (s) => PrimaryFixedDim(),
          secondBackground: (s) => PrimaryFixed(),
          contrastCurve: (s) => new ContrastCurve(4.5, 7, 11, 21)
    );
    }

    public virtual DynamicColor OnPrimaryFixedVariant()
    {
        return DynamicColor.FromPalette(
          name: "on_primary_fixed_variant",
          palette: (s) => s.PrimaryPalette,
          tone: (s) => IsMonochrome(s) ? 90.0 : 30.0,
          background: (s) => PrimaryFixedDim(),
          secondBackground: (s) => PrimaryFixed(),
          contrastCurve: (s) => new ContrastCurve(3, 4.5, 7, 11)
    );
    }

    ///////////////////////////////////////////////////////////////////
    // Secondary Fixed [QF]                                          //
    ///////////////////////////////////////////////////////////////////

    public virtual DynamicColor SecondaryFixed()
    {
        return DynamicColor.FromPalette(
          name: "secondary_fixed",
          palette: (s) => s.SecondaryPalette,
          tone: (s) => IsMonochrome(s) ? 80.0 : 90.0,
          isBackground: true,
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => new ContrastCurve(1, 1, 3, 4.5),
          toneDeltaPair: (s) => new ToneDeltaPair(
              SecondaryFixed(), SecondaryFixedDim(), 10, TonePolarity.Lighter, true)
    );
    }

    public virtual DynamicColor SecondaryFixedDim()
    {
        return DynamicColor.FromPalette(
          name: "secondary_fixed_dim",
          palette: (s) => s.SecondaryPalette,
          tone: (s) => IsMonochrome(s) ? 70.0 : 80.0,
          isBackground: true,
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => new ContrastCurve(1, 1, 3, 4.5),
          toneDeltaPair: (s) => new ToneDeltaPair(
              SecondaryFixed(), SecondaryFixedDim(), 10, TonePolarity.Lighter, true)
    );
    }

    public virtual DynamicColor OnSecondaryFixed()
    {
        return DynamicColor.FromPalette(
          name: "on_secondary_fixed",
          palette: (s) => s.SecondaryPalette,
          tone: (s) => 10.0,
          background: (s) => SecondaryFixedDim(),
          secondBackground: (s) => SecondaryFixed(),
          contrastCurve: (s) => new ContrastCurve(4.5, 7, 11, 21)
    );
    }

    public virtual DynamicColor OnSecondaryFixedVariant()
    {
        return DynamicColor.FromPalette(
          name: "on_secondary_fixed_variant",
          palette: (s) => s.SecondaryPalette,
          tone: (s) => IsMonochrome(s) ? 25.0 : 30.0,
          background: (s) => SecondaryFixedDim(),
          secondBackground: (s) => SecondaryFixed(),
          contrastCurve: (s) => new ContrastCurve(3, 4.5, 7, 11)
    );
    }

    /////////////////////////////////////////////////////////////////
    // Tertiary Fixed [TF]                                         //
    /////////////////////////////////////////////////////////////////

    public virtual DynamicColor TertiaryFixed()
    {
        return DynamicColor.FromPalette(
          name: "tertiary_fixed",
          palette: (s) => s.TertiaryPalette,
          tone: (s) => IsMonochrome(s) ? 40.0 : 90.0,
          isBackground: true,
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => new ContrastCurve(1, 1, 3, 4.5),
          toneDeltaPair: (s) => new ToneDeltaPair(
              TertiaryFixed(), TertiaryFixedDim(), 10, TonePolarity.Lighter, true)
    );
    }

    public virtual DynamicColor TertiaryFixedDim()
    {
        return DynamicColor.FromPalette(
          name: "tertiary_fixed_dim",
          palette: (s) => s.TertiaryPalette,
          tone: (s) => IsMonochrome(s) ? 30.0 : 80.0,
          isBackground: true,
          background: (s) => HighestSurface(s),
          contrastCurve: (s) => new ContrastCurve(1, 1, 3, 4.5),
          toneDeltaPair: (s) => new ToneDeltaPair(
              TertiaryFixed(), TertiaryFixedDim(), 10, TonePolarity.Lighter, true)
    );
    }

    public virtual DynamicColor OnTertiaryFixed()
    {
        return DynamicColor.FromPalette(
          name: "on_tertiary_fixed",
          palette: (s) => s.TertiaryPalette,
          tone: (s) => IsMonochrome(s) ? 100.0 : 10.0,
          background: (s) => TertiaryFixedDim(),
          secondBackground: (s) => TertiaryFixed(),
          contrastCurve: (s) => new ContrastCurve(4.5, 7, 11, 21)
    );
    }

    public virtual DynamicColor OnTertiaryFixedVariant()
    {
        return DynamicColor.FromPalette(
          name: "on_tertiary_fixed_variant",
          palette: (s) => s.TertiaryPalette,
          tone: (s) => IsMonochrome(s) ? 90.0 : 30.0,
          background: (s) => TertiaryFixedDim(),
          secondBackground: (s) => TertiaryFixed(),
          contrastCurve: (s) => new ContrastCurve(3, 4.5, 7, 11)
    );
    }

    ////////////////////////////////////////////////////////////////
    // Other                                                      //
    ////////////////////////////////////////////////////////////////

    public virtual DynamicColor HighestSurface(DynamicScheme s)
    {
        return s.IsDark ? SurfaceBright() : SurfaceDim();
    }
}
