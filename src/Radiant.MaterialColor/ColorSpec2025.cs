// Copyright 2025 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md), translated mechanically from its
// TypeScript and verified against its outputs; the structure deliberately mirrors upstream's so a
// newer upstream can be diffed and ported the same way.

namespace Radiant.MaterialColor;

/// <summary>The Material color roles as the 2025 spec defines them. Upstream's <c>ColorSpecDelegateImpl2025</c>.</summary>
internal class ColorSpec2025 : ColorSpec2021
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


    ////////////////////////////////////////////////////////////////
    // Surfaces [S]                                               //
    ////////////////////////////////////////////////////////////////

    public override DynamicColor Surface()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "surface",
          palette: (s) => s.NeutralPalette,
          tone: (s) =>
          {
              base.Surface().Tone(s);
              if (s.Platform == Platform.Phone)
              {
                  if (s.IsDark)
                  {
                      return 4;
                  }
                  else
                  {
                      if (Hct.IsYellow(s.NeutralPalette.Hue))
                      {
                          return 99;
                      }
                      else if (s.Variant == Variant.Vibrant)
                      {
                          return 97;
                      }
                      else
                      {
                          return 98;
                      }
                  }
              }
              else
              {
                  return 0;
              }
          },
          isBackground: true
    );
        return DynamicColor.ExtendSpecVersion(base.Surface(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor SurfaceDim()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "surface_dim",
          palette: (s) => s.NeutralPalette,
          tone: (s) =>
          {
              if (s.IsDark)
              {
                  return 4;
              }
              else
              {
                  if (Hct.IsYellow(s.NeutralPalette.Hue))
                  {
                      return 90;
                  }
                  else if (s.Variant == Variant.Vibrant)
                  {
                      return 85;
                  }
                  else
                  {
                      return 87;
                  }
              }
          },
          isBackground: true,
          chromaMultiplier: (s) =>
          {
              if (!s.IsDark)
              {
                  if (s.Variant == Variant.Neutral)
                  {
                      return 2.5;
                  }
                  else if (s.Variant == Variant.TonalSpot)
                  {
                      return 1.7;
                  }
                  else if (s.Variant == Variant.Expressive)
                  {
                      return Hct.IsYellow(s.NeutralPalette.Hue) ? 2.7 : 1.75;
                  }
                  else if (s.Variant == Variant.Vibrant)
                  {
                      return 1.36;
                  }
              }
              return 1;
          }
    );
        return DynamicColor.ExtendSpecVersion(base.SurfaceDim(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor SurfaceBright()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "surface_bright",
          palette: (s) => s.NeutralPalette,
          tone: (s) =>
          {
              if (s.IsDark)
              {
                  return 18;
              }
              else
              {
                  if (Hct.IsYellow(s.NeutralPalette.Hue))
                  {
                      return 99;
                  }
                  else if (s.Variant == Variant.Vibrant)
                  {
                      return 97;
                  }
                  else
                  {
                      return 98;
                  }
              }
          },
          isBackground: true,
          chromaMultiplier: (s) =>
          {
              if (s.IsDark)
              {
                  if (s.Variant == Variant.Neutral)
                  {
                      return 2.5;
                  }
                  else if (s.Variant == Variant.TonalSpot)
                  {
                      return 1.7;
                  }
                  else if (s.Variant == Variant.Expressive)
                  {
                      return Hct.IsYellow(s.NeutralPalette.Hue) ? 2.7 : 1.75;
                  }
                  else if (s.Variant == Variant.Vibrant)
                  {
                      return 1.36;
                  }
              }
              return 1;
          }
    );
        return DynamicColor.ExtendSpecVersion(base.SurfaceBright(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor SurfaceContainerLowest()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "surface_container_lowest",
          palette: (s) => s.NeutralPalette,
          tone: (s) => s.IsDark ? 0 : 100,
          isBackground: true
    );
        return DynamicColor.ExtendSpecVersion(base.SurfaceContainerLowest(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor SurfaceContainerLow()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "surface_container_low",
          palette: (s) => s.NeutralPalette,
          tone: (s) =>
          {
              if (s.Platform == Platform.Phone)
              {
                  if (s.IsDark)
                  {
                      return 6;
                  }
                  else
                  {
                      if (Hct.IsYellow(s.NeutralPalette.Hue))
                      {
                          return 98;
                      }
                      else if (s.Variant == Variant.Vibrant)
                      {
                          return 95;
                      }
                      else
                      {
                          return 96;
                      }
                  }
              }
              else
              {
                  return 15;
              }
          },
          isBackground: true,
          chromaMultiplier: (s) =>
          {
              if (s.Platform == Platform.Phone)
              {
                  if (s.Variant == Variant.Neutral)
                  {
                      return 1.3;
                  }
                  else if (s.Variant == Variant.TonalSpot)
                  {
                      return 1.25;
                  }
                  else if (s.Variant == Variant.Expressive)
                  {
                      return Hct.IsYellow(s.NeutralPalette.Hue) ? 1.3 : 1.15;
                  }
                  else if (s.Variant == Variant.Vibrant)
                  {
                      return 1.08;
                  }
              }
              return 1;
          }
    );
        return DynamicColor.ExtendSpecVersion(base.SurfaceContainerLow(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor SurfaceContainer()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "surface_container",
          palette: (s) => s.NeutralPalette,
          tone: (s) =>
          {
              if (s.Platform == Platform.Phone)
              {
                  if (s.IsDark)
                  {
                      return 9;
                  }
                  else
                  {
                      if (Hct.IsYellow(s.NeutralPalette.Hue))
                      {
                          return 96;
                      }
                      else if (s.Variant == Variant.Vibrant)
                      {
                          return 92;
                      }
                      else
                      {
                          return 94;
                      }
                  }
              }
              else
              {
                  return 20;
              }
          },
          isBackground: true,
          chromaMultiplier: (s) =>
          {
              if (s.Platform == Platform.Phone)
              {
                  if (s.Variant == Variant.Neutral)
                  {
                      return 1.6;
                  }
                  else if (s.Variant == Variant.TonalSpot)
                  {
                      return 1.4;
                  }
                  else if (s.Variant == Variant.Expressive)
                  {
                      return Hct.IsYellow(s.NeutralPalette.Hue) ? 1.6 : 1.3;
                  }
                  else if (s.Variant == Variant.Vibrant)
                  {
                      return 1.15;
                  }
              }
              return 1;
          }
    );
        return DynamicColor.ExtendSpecVersion(base.SurfaceContainer(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor SurfaceContainerHigh()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "surface_container_high",
          palette: (s) => s.NeutralPalette,
          tone: (s) =>
          {
              if (s.Platform == Platform.Phone)
              {
                  if (s.IsDark)
                  {
                      return 12;
                  }
                  else
                  {
                      if (Hct.IsYellow(s.NeutralPalette.Hue))
                      {
                          return 94;
                      }
                      else if (s.Variant == Variant.Vibrant)
                      {
                          return 90;
                      }
                      else
                      {
                          return 92;
                      }
                  }
              }
              else
              {
                  return 25;
              }
          },
          isBackground: true,
          chromaMultiplier: (s) =>
          {
              if (s.Platform == Platform.Phone)
              {
                  if (s.Variant == Variant.Neutral)
                  {
                      return 1.9;
                  }
                  else if (s.Variant == Variant.TonalSpot)
                  {
                      return 1.5;
                  }
                  else if (s.Variant == Variant.Expressive)
                  {
                      return Hct.IsYellow(s.NeutralPalette.Hue) ? 1.95 : 1.45;
                  }
                  else if (s.Variant == Variant.Vibrant)
                  {
                      return 1.22;
                  }
              }
              return 1;
          }
    );
        return DynamicColor.ExtendSpecVersion(base.SurfaceContainerHigh(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor SurfaceContainerHighest()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "surface_container_highest",
          palette: (s) => s.NeutralPalette,
          tone: (s) =>
          {
              if (s.IsDark)
              {
                  return 15;
              }
              else
              {
                  if (Hct.IsYellow(s.NeutralPalette.Hue))
                  {
                      return 92;
                  }
                  else if (s.Variant == Variant.Vibrant)
                  {
                      return 88;
                  }
                  else
                  {
                      return 90;
                  }
              }
          },
          isBackground: true,
          chromaMultiplier: (s) =>
          {
              if (s.Variant == Variant.Neutral)
              {
                  return 2.2;
              }
              else if (s.Variant == Variant.TonalSpot)
              {
                  return 1.7;
              }
              else if (s.Variant == Variant.Expressive)
              {
                  return Hct.IsYellow(s.NeutralPalette.Hue) ? 2.3 : 1.6;
              }
              else if (s.Variant == Variant.Vibrant)
              {
                  return 1.29;
              }
              else
              {  // default
                  return 1;
              }
          }
    );
        return DynamicColor.ExtendSpecVersion(
            base.SurfaceContainerHighest(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor OnSurface()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "on_surface",
          palette: (s) => s.NeutralPalette,
          tone: (s) =>
          {
              if (s.Variant == Variant.Vibrant)
              {
                  return TMaxC(s.NeutralPalette, 0, 100, 1.1);
              }
              else
              {
                  // For all other variants, the initial tone should be the default
                  // tone, which is the same as the background color.
                  return DynamicColor.GetInitialToneFromBackground(
                  (s) => s.Platform == Platform.Phone ? HighestSurface(s) :
                                                  SurfaceContainerHigh())(s);
              }
          },
          chromaMultiplier: (s) =>
          {
              if (s.Platform == Platform.Phone)
              {
                  if (s.Variant == Variant.Neutral)
                  {
                      return 2.2;
                  }
                  else if (s.Variant == Variant.TonalSpot)
                  {
                      return 1.7;
                  }
                  else if (s.Variant == Variant.Expressive)
                  {
                      return Hct.IsYellow(s.NeutralPalette.Hue) ? (s.IsDark ? 3.0 : 2.3) :
                                                              1.6;
                  }
              }
              return 1;
          },
          background: (s) => s.Platform == Platform.Phone ? HighestSurface(s) :
                                                      SurfaceContainerHigh(),
          contrastCurve: (s) =>
              s.IsDark && s.Platform == Platform.Phone ? GetCurve(11) : GetCurve(9)
    );
        return DynamicColor.ExtendSpecVersion(base.OnSurface(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor OnSurfaceVariant()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "on_surface_variant",
          palette: (s) => s.NeutralPalette,
          chromaMultiplier: (s) =>
          {
              if (s.Platform == Platform.Phone)
              {
                  if (s.Variant == Variant.Neutral)
                  {
                      return 2.2;
                  }
                  else if (s.Variant == Variant.TonalSpot)
                  {
                      return 1.7;
                  }
                  else if (s.Variant == Variant.Expressive)
                  {
                      return Hct.IsYellow(s.NeutralPalette.Hue) ? (s.IsDark ? 3.0 : 2.3) :
                                                              1.6;
                  }
              }
              return 1;
          },
          background: (s) => s.Platform == Platform.Phone ? HighestSurface(s) :
                                                      SurfaceContainerHigh(),
          contrastCurve: (s) => s.Platform == Platform.Phone ?
              (s.IsDark ? GetCurve(6) : GetCurve(4.5)) :
              GetCurve(7)
    );
        return DynamicColor.ExtendSpecVersion(base.OnSurfaceVariant(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor Outline()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "outline",
          palette: (s) => s.NeutralPalette,
          chromaMultiplier: (s) =>
          {
              if (s.Platform == Platform.Phone)
              {
                  if (s.Variant == Variant.Neutral)
                  {
                      return 2.2;
                  }
                  else if (s.Variant == Variant.TonalSpot)
                  {
                      return 1.7;
                  }
                  else if (s.Variant == Variant.Expressive)
                  {
                      return Hct.IsYellow(s.NeutralPalette.Hue) ? (s.IsDark ? 3.0 : 2.3) :
                                                              1.6;
                  }
              }
              return 1;
          },
          background: (s) => s.Platform == Platform.Phone ? HighestSurface(s) :
                                                      SurfaceContainerHigh(),
          contrastCurve: (s) =>
              s.Platform == Platform.Phone ? GetCurve(3) : GetCurve(4.5)
    );
        return DynamicColor.ExtendSpecVersion(base.Outline(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor OutlineVariant()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "outline_variant",
          palette: (s) => s.NeutralPalette,
          chromaMultiplier: (s) =>
          {
              if (s.Platform == Platform.Phone)
              {
                  if (s.Variant == Variant.Neutral)
                  {
                      return 2.2;
                  }
                  else if (s.Variant == Variant.TonalSpot)
                  {
                      return 1.7;
                  }
                  else if (s.Variant == Variant.Expressive)
                  {
                      return Hct.IsYellow(s.NeutralPalette.Hue) ? (s.IsDark ? 3.0 : 2.3) :
                                                              1.6;
                  }
              }
              return 1;
          },
          background: (s) => s.Platform == Platform.Phone ? HighestSurface(s) :
                                                      SurfaceContainerHigh(),
          contrastCurve: (s) =>
              s.Platform == Platform.Phone ? GetCurve(1.5) : GetCurve(3)
    );
        return DynamicColor.ExtendSpecVersion(base.OutlineVariant(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor InverseSurface()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "inverse_surface",
          palette: (s) => s.NeutralPalette,
          tone: (s) => s.IsDark ? 98 : 4,
          isBackground: true
    );
        return DynamicColor.ExtendSpecVersion(base.InverseSurface(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor InverseOnSurface()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "inverse_on_surface",
          palette: (s) => s.NeutralPalette,
          background: (s) => InverseSurface(),
          contrastCurve: (s) => GetCurve(7)
    );
        return DynamicColor.ExtendSpecVersion(base.InverseOnSurface(), SpecVersion.Spec2025, color2025);
    }

    ////////////////////////////////////////////////////////////////
    // Primaries [P]                                              //
    ////////////////////////////////////////////////////////////////

    public override DynamicColor Primary()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "primary",
          palette: (s) => s.PrimaryPalette,
          tone: (s) =>
          {
              if (s.Variant == Variant.Neutral)
              {
                  if (s.Platform == Platform.Phone)
                  {
                      return s.IsDark ? 80 : 40;
                  }
                  else
                  {
                      return 90;
                  }
              }
              else if (s.Variant == Variant.TonalSpot)
              {
                  if (s.Platform == Platform.Phone)
                  {
                      if (s.IsDark)
                      {
                          return 80;
                      }
                      else
                      {
                          return TMaxC(s.PrimaryPalette);
                      }
                  }
                  else
                  {
                      return TMaxC(s.PrimaryPalette, 0, 90);
                  }
              }
              else if (s.Variant == Variant.Expressive)
              {
                  if (s.Platform == Platform.Phone)
                  {
                      return TMaxC(
                      s.PrimaryPalette, 0,
                      s.IsDark ? (Hct.IsCyan(s.PrimaryPalette.Hue) ? 88 : 98) :
                                 (Hct.IsYellow(s.PrimaryPalette.Hue) ? 25 : 98));
                  }
                  else
                  {  // WATCH
                      return TMaxC(s.PrimaryPalette);
                  }
              }
              else
              {  // VIBRANT
                  if (s.Platform == Platform.Phone)
                  {
                      return TMaxC(
                      s.PrimaryPalette, 0,
                      Hct.IsCyan(s.PrimaryPalette.Hue) ? 88 : 98);
                  }
                  else
                  {  // WATCH
                      return TMaxC(s.PrimaryPalette);
                  }
              }
          },
          isBackground: true,
          background: (s) => s.Platform == Platform.Phone ? HighestSurface(s) :
                                                      SurfaceContainerHigh(),
          contrastCurve: (s) =>
              s.Platform == Platform.Phone ? GetCurve(4.5) : GetCurve(7),
          toneDeltaPair: (s) => s.Platform == Platform.Phone ?
              new ToneDeltaPair(
                  PrimaryContainer(), Primary(), 5, TonePolarity.RelativeLighter,
                  true, DeltaConstraint.Farther) :
              null
    );
        return DynamicColor.ExtendSpecVersion(base.Primary(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor PrimaryDim()
    {
        return DynamicColor.FromPalette(
          name: "primary_dim",
          palette: (s) => s.PrimaryPalette,
          tone: (s) =>
          {
              if (s.Variant == Variant.Neutral)
              {
                  return 85;
              }
              else if (s.Variant == Variant.TonalSpot)
              {
                  return TMaxC(s.PrimaryPalette, 0, 90);
              }
              else
              {
                  return TMaxC(s.PrimaryPalette);
              }
          },
          isBackground: true,
          background: (s) => SurfaceContainerHigh(),
          contrastCurve: (s) => GetCurve(4.5),
          toneDeltaPair: (s) => new ToneDeltaPair(
              PrimaryDim(), Primary(), 5, TonePolarity.Darker, true, DeltaConstraint.Farther)
    );
    }

    public override DynamicColor OnPrimary()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "on_primary",
          palette: (s) => s.PrimaryPalette,
          background: (s) =>
              s.Platform == Platform.Phone ? Primary() : PrimaryDim(),
          contrastCurve: (s) => s.Platform == Platform.Phone ? GetCurve(6) : GetCurve(7)
    );
        return DynamicColor.ExtendSpecVersion(base.OnPrimary(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor PrimaryContainer()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "primary_container",
          palette: (s) => s.PrimaryPalette,
          tone: (s) =>
          {
              if (s.Platform == Platform.Watch)
              {
                  return 30;
              }
              else if (s.Variant == Variant.Neutral)
              {
                  return s.IsDark ? 30 : 90;
              }
              else if (s.Variant == Variant.TonalSpot)
              {
                  return s.IsDark ? TMinC(s.PrimaryPalette, 35, 93) :
                                TMaxC(s.PrimaryPalette, 0, 90);
              }
              else if (s.Variant == Variant.Expressive)
              {
                  return s.IsDark ? TMinC(s.PrimaryPalette, 30, 93) :
                                TMaxC(
                                    s.PrimaryPalette, 78,
                                    Hct.IsCyan(s.PrimaryPalette.Hue) ? 88 : 90);
              }
              else
              {  // VIBRANT
                  return s.IsDark ? TMinC(s.PrimaryPalette, 66, 93) :
                                TMaxC(
                                    s.PrimaryPalette, 66,
                                    Hct.IsCyan(s.PrimaryPalette.Hue) ? 88 : 93);
              }
          },
          isBackground: true,
          background: (s) =>
              s.Platform == Platform.Phone ? HighestSurface(s) : null,
          toneDeltaPair: (s) => s.Platform == Platform.Phone ?
              null :
              new ToneDeltaPair(
                  PrimaryContainer(), PrimaryDim(), 10, TonePolarity.Darker, true,
                  DeltaConstraint.Farther),
          contrastCurve: (s) => s.Platform == Platform.Phone && s.ContrastLevel > 0 ?
              GetCurve(1.5) :
              null
    );
        return DynamicColor.ExtendSpecVersion(base.PrimaryContainer(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor OnPrimaryContainer()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "on_primary_container",
          palette: (s) => s.PrimaryPalette,
          background: (s) => PrimaryContainer(),
          contrastCurve: (s) => s.Platform == Platform.Phone ? GetCurve(6) : GetCurve(7)
    );
        return DynamicColor.ExtendSpecVersion(base.OnPrimaryContainer(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor PrimaryFixed()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "primary_fixed",
          palette: (s) => s.PrimaryPalette,
          tone: (s) =>
          {
              var tempS = s.CloneWith(isDark: false, contrastLevel: 0);
              return PrimaryContainer().GetTone(tempS);
          },
          isBackground: true,
          background: (s) =>
              s.Platform == Platform.Phone ? HighestSurface(s) : null,
          contrastCurve: (s) => s.Platform == Platform.Phone && s.ContrastLevel > 0 ?
              GetCurve(1.5) :
              null
    );
        return DynamicColor.ExtendSpecVersion(base.PrimaryFixed(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor PrimaryFixedDim()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "primary_fixed_dim",
          palette: (s) => s.PrimaryPalette,
          tone: (s) => PrimaryFixed().GetTone(s),
          isBackground: true,
          toneDeltaPair: (s) => new ToneDeltaPair(
              PrimaryFixedDim(), PrimaryFixed(), 5, TonePolarity.Darker, true,
              DeltaConstraint.Exact)
    );
        return DynamicColor.ExtendSpecVersion(base.PrimaryFixedDim(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor OnPrimaryFixed()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "on_primary_fixed",
          palette: (s) => s.PrimaryPalette,
          background: (s) => PrimaryFixedDim(),
          contrastCurve: (s) => GetCurve(7)
    );
        return DynamicColor.ExtendSpecVersion(base.OnPrimaryFixed(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor OnPrimaryFixedVariant()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "on_primary_fixed_variant",
          palette: (s) => s.PrimaryPalette,
          background: (s) => PrimaryFixedDim(),
          contrastCurve: (s) => GetCurve(4.5)
    );
        return DynamicColor.ExtendSpecVersion(base.OnPrimaryFixedVariant(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor InversePrimary()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "inverse_primary",
          palette: (s) => s.PrimaryPalette,
          tone: (s) => TMaxC(s.PrimaryPalette),
          background: (s) => InverseSurface(),
          contrastCurve: (s) => s.Platform == Platform.Phone ? GetCurve(6) : GetCurve(7)
    );
        return DynamicColor.ExtendSpecVersion(base.InversePrimary(), SpecVersion.Spec2025, color2025);
    }

    ////////////////////////////////////////////////////////////////
    // Secondaries [Q]                                            //
    ////////////////////////////////////////////////////////////////

    public override DynamicColor Secondary()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "secondary",
          palette: (s) => s.SecondaryPalette,
          tone: (s) =>
          {
              if (s.Platform == Platform.Watch)
              {
                  return s.Variant == Variant.Neutral ?
                  90 :
                  TMaxC(s.SecondaryPalette, 0, 90);
              }
              else if (s.Variant == Variant.Neutral)
              {
                  return s.IsDark ? TMinC(s.SecondaryPalette, 0, 98) :
                                TMaxC(s.SecondaryPalette);
              }
              else if (s.Variant == Variant.Vibrant)
              {
                  return TMaxC(s.SecondaryPalette, 0, s.IsDark ? 90 : 98);
              }
              else
              {  // EXPRESSIVE and TONAL_SPOT
                  return s.IsDark ? 80 : TMaxC(s.SecondaryPalette);
              }
          },
          isBackground: true,
          background: (s) => s.Platform == Platform.Phone ? HighestSurface(s) :
                                                      SurfaceContainerHigh(),
          contrastCurve: (s) =>
              s.Platform == Platform.Phone ? GetCurve(4.5) : GetCurve(7),
          toneDeltaPair: (s) => s.Platform == Platform.Phone ?
              new ToneDeltaPair(
                  SecondaryContainer(), Secondary(), 5,
                  TonePolarity.RelativeLighter, true, DeltaConstraint.Farther) :
              null
    );
        return DynamicColor.ExtendSpecVersion(base.Secondary(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor SecondaryDim()
    {
        return DynamicColor.FromPalette(
          name: "secondary_dim",
          palette: (s) => s.SecondaryPalette,
          tone: (s) =>
          {
              if (s.Variant == Variant.Neutral)
              {
                  return 85;
              }
              else
              {
                  return TMaxC(s.SecondaryPalette, 0, 90);
              }
          },
          isBackground: true,
          background: (s) => SurfaceContainerHigh(),
          contrastCurve: (s) => GetCurve(4.5),
          toneDeltaPair: (s) => new ToneDeltaPair(
              SecondaryDim(), Secondary(), 5, TonePolarity.Darker, true, DeltaConstraint.Farther)
    );
    }

    public override DynamicColor OnSecondary()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "on_secondary",
          palette: (s) => s.SecondaryPalette,
          background: (s) =>
              s.Platform == Platform.Phone ? Secondary() : SecondaryDim(),
          contrastCurve: (s) => s.Platform == Platform.Phone ? GetCurve(6) : GetCurve(7)
    );
        return DynamicColor.ExtendSpecVersion(base.OnSecondary(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor SecondaryContainer()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "secondary_container",
          palette: (s) => s.SecondaryPalette,
          tone: (s) =>
          {
              if (s.Platform == Platform.Watch)
              {
                  return 30;
              }
              else if (s.Variant == Variant.Vibrant)
              {
                  return s.IsDark ? TMinC(s.SecondaryPalette, 30, 40) :
                                TMaxC(s.SecondaryPalette, 84, 90);
              }
              else if (s.Variant == Variant.Expressive)
              {
                  return s.IsDark ? 15 : TMaxC(s.SecondaryPalette, 90, 95);
              }
              else
              {
                  return s.IsDark ? 25 : 90;
              }
          },
          isBackground: true,
          background: (s) =>
              s.Platform == Platform.Phone ? HighestSurface(s) : null,
          toneDeltaPair: (s) => s.Platform == Platform.Watch ?
              new ToneDeltaPair(
                  SecondaryContainer(), SecondaryDim(), 10, TonePolarity.Darker,
                  true, DeltaConstraint.Farther) :
              null,
          contrastCurve: (s) => s.Platform == Platform.Phone && s.ContrastLevel > 0 ?
              GetCurve(1.5) :
              null
    );
        return DynamicColor.ExtendSpecVersion(base.SecondaryContainer(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor OnSecondaryContainer()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "on_secondary_container",
          palette: (s) => s.SecondaryPalette,
          background: (s) => SecondaryContainer(),
          contrastCurve: (s) => s.Platform == Platform.Phone ? GetCurve(6) : GetCurve(7)
    );
        return DynamicColor.ExtendSpecVersion(base.OnSecondaryContainer(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor SecondaryFixed()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "secondary_fixed",
          palette: (s) => s.SecondaryPalette,
          tone: (s) =>
          {
              var tempS = s.CloneWith(isDark: false, contrastLevel: 0);
              return SecondaryContainer().GetTone(tempS);
          },
          isBackground: true,
          background: (s) =>
              s.Platform == Platform.Phone ? HighestSurface(s) : null,
          contrastCurve: (s) => s.Platform == Platform.Phone && s.ContrastLevel > 0 ?
              GetCurve(1.5) :
              null
    );
        return DynamicColor.ExtendSpecVersion(base.SecondaryFixed(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor SecondaryFixedDim()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "secondary_fixed_dim",
          palette: (s) => s.SecondaryPalette,
          tone: (s) => SecondaryFixed().GetTone(s),
          isBackground: true,
          toneDeltaPair: (s) => new ToneDeltaPair(
              SecondaryFixedDim(), SecondaryFixed(), 5, TonePolarity.Darker, true,
              DeltaConstraint.Exact)
    );
        return DynamicColor.ExtendSpecVersion(base.SecondaryFixedDim(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor OnSecondaryFixed()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "on_secondary_fixed",
          palette: (s) => s.SecondaryPalette,
          background: (s) => SecondaryFixedDim(),
          contrastCurve: (s) => GetCurve(7)
    );
        return DynamicColor.ExtendSpecVersion(base.OnSecondaryFixed(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor OnSecondaryFixedVariant()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "on_secondary_fixed_variant",
          palette: (s) => s.SecondaryPalette,
          background: (s) => SecondaryFixedDim(),
          contrastCurve: (s) => GetCurve(4.5)
    );
        return DynamicColor.ExtendSpecVersion(
            base.OnSecondaryFixedVariant(), SpecVersion.Spec2025, color2025);
    }

    ////////////////////////////////////////////////////////////////
    // Tertiaries [T]                                             //
    ////////////////////////////////////////////////////////////////

    public override DynamicColor Tertiary()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "tertiary",
          palette: (s) => s.TertiaryPalette,
          tone: (s) =>
          {
              if (s.Platform == Platform.Watch)
              {
                  return s.Variant == Variant.TonalSpot ?
                  TMaxC(s.TertiaryPalette, 0, 90) :
                  TMaxC(s.TertiaryPalette);
              }
              else if (
              s.Variant == Variant.Expressive || s.Variant == Variant.Vibrant)
              {
                  return TMaxC(
                  s.TertiaryPalette, 0,
                  Hct.IsCyan(s.TertiaryPalette.Hue) ? 88 : (s.IsDark ? 98 : 100));
              }
              else
              {  // NEUTRAL and TONAL_SPOT
                  return s.IsDark ? TMaxC(s.TertiaryPalette, 0, 98) :
                                TMaxC(s.TertiaryPalette);
              }
          },
          isBackground: true,
          background: (s) => s.Platform == Platform.Phone ? HighestSurface(s) :
                                                      SurfaceContainerHigh(),
          contrastCurve: (s) =>
              s.Platform == Platform.Phone ? GetCurve(4.5) : GetCurve(7),
          toneDeltaPair: (s) => s.Platform == Platform.Phone ?
              new ToneDeltaPair(
                  TertiaryContainer(), Tertiary(), 5, TonePolarity.RelativeLighter,
                  true, DeltaConstraint.Farther) :
              null
    );
        return DynamicColor.ExtendSpecVersion(base.Tertiary(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor TertiaryDim()
    {
        return DynamicColor.FromPalette(
          name: "tertiary_dim",
          palette: (s) => s.TertiaryPalette,
          tone: (s) =>
          {
              if (s.Variant == Variant.TonalSpot)
              {
                  return TMaxC(s.TertiaryPalette, 0, 90);
              }
              else
              {
                  return TMaxC(s.TertiaryPalette);
              }
          },
          isBackground: true,
          background: (s) => SurfaceContainerHigh(),
          contrastCurve: (s) => GetCurve(4.5),
          toneDeltaPair: (s) => new ToneDeltaPair(
              TertiaryDim(), Tertiary(), 5, TonePolarity.Darker, true, DeltaConstraint.Farther)
    );
    }

    public override DynamicColor OnTertiary()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "on_tertiary",
          palette: (s) => s.TertiaryPalette,
          background: (s) =>
              s.Platform == Platform.Phone ? Tertiary() : TertiaryDim(),
          contrastCurve: (s) => s.Platform == Platform.Phone ? GetCurve(6) : GetCurve(7)
    );
        return DynamicColor.ExtendSpecVersion(base.OnTertiary(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor TertiaryContainer()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "tertiary_container",
          palette: (s) => s.TertiaryPalette,
          tone: (s) =>
          {
              if (s.Platform == Platform.Watch)
              {
                  return s.Variant == Variant.TonalSpot ?
                  TMaxC(s.TertiaryPalette, 0, 90) :
                  TMaxC(s.TertiaryPalette);
              }
              else
              {
                  if (s.Variant == Variant.Neutral)
                  {
                      return s.IsDark ? TMaxC(s.TertiaryPalette, 0, 93) :
                                    TMaxC(s.TertiaryPalette, 0, 96);
                  }
                  else if (s.Variant == Variant.TonalSpot)
                  {
                      return TMaxC(s.TertiaryPalette, 0, s.IsDark ? 93 : 100);
                  }
                  else if (s.Variant == Variant.Expressive)
                  {
                      return TMaxC(
                      s.TertiaryPalette, 75,
                      Hct.IsCyan(s.TertiaryPalette.Hue) ? 88 : (s.IsDark ? 93 : 100));
                  }
                  else
                  {  // VIBRANT
                      return s.IsDark ? TMaxC(s.TertiaryPalette, 0, 93) :
                                    TMaxC(s.TertiaryPalette, 72, 100);
                  }
              }
          },
          isBackground: true,
          background: (s) =>
              s.Platform == Platform.Phone ? HighestSurface(s) : null,
          toneDeltaPair: (s) => s.Platform == Platform.Watch ?
              new ToneDeltaPair(
                  TertiaryContainer(), TertiaryDim(), 10, TonePolarity.Darker, true,
                  DeltaConstraint.Farther) :
              null,
          contrastCurve: (s) => s.Platform == Platform.Phone && s.ContrastLevel > 0 ?
              GetCurve(1.5) :
              null
    );
        return DynamicColor.ExtendSpecVersion(base.TertiaryContainer(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor OnTertiaryContainer()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "on_tertiary_container",
          palette: (s) => s.TertiaryPalette,
          background: (s) => TertiaryContainer(),
          contrastCurve: (s) => s.Platform == Platform.Phone ? GetCurve(6) : GetCurve(7)
    );
        return DynamicColor.ExtendSpecVersion(base.OnTertiaryContainer(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor TertiaryFixed()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "tertiary_fixed",
          palette: (s) => s.TertiaryPalette,
          tone: (s) =>
          {
              var tempS = s.CloneWith(isDark: false, contrastLevel: 0);
              return TertiaryContainer().GetTone(tempS);
          },
          isBackground: true,
          background: (s) =>
              s.Platform == Platform.Phone ? HighestSurface(s) : null,
          contrastCurve: (s) => s.Platform == Platform.Phone && s.ContrastLevel > 0 ?
              GetCurve(1.5) :
              null
    );
        return DynamicColor.ExtendSpecVersion(base.TertiaryFixed(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor TertiaryFixedDim()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "tertiary_fixed_dim",
          palette: (s) => s.TertiaryPalette,
          tone: (s) => TertiaryFixed().GetTone(s),
          isBackground: true,
          toneDeltaPair: (s) => new ToneDeltaPair(
              TertiaryFixedDim(), TertiaryFixed(), 5, TonePolarity.Darker, true,
              DeltaConstraint.Exact)
    );
        return DynamicColor.ExtendSpecVersion(base.TertiaryFixedDim(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor OnTertiaryFixed()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "on_tertiary_fixed",
          palette: (s) => s.TertiaryPalette,
          background: (s) => TertiaryFixedDim(),
          contrastCurve: (s) => GetCurve(7)
    );
        return DynamicColor.ExtendSpecVersion(base.OnTertiaryFixed(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor OnTertiaryFixedVariant()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "on_tertiary_fixed_variant",
          palette: (s) => s.TertiaryPalette,
          background: (s) => TertiaryFixedDim(),
          contrastCurve: (s) => GetCurve(4.5)
    );
        return DynamicColor.ExtendSpecVersion(base.OnTertiaryFixedVariant(), SpecVersion.Spec2025, color2025);
    }

    ////////////////////////////////////////////////////////////////
    // Errors [E]                                                 //
    ////////////////////////////////////////////////////////////////

    public override DynamicColor Error()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "error",
          palette: (s) => s.ErrorPalette,
          tone: (s) =>
          {
              if (s.Platform == Platform.Phone)
              {
                  return s.IsDark ? TMinC(s.ErrorPalette, 0, 98) :
                                TMaxC(s.ErrorPalette);
              }
              else
              {
                  return TMinC(s.ErrorPalette);
              }
          },
          isBackground: true,
          background: (s) => s.Platform == Platform.Phone ? HighestSurface(s) :
                                                      SurfaceContainerHigh(),
          contrastCurve: (s) =>
              s.Platform == Platform.Phone ? GetCurve(4.5) : GetCurve(7),
          toneDeltaPair: (s) => s.Platform == Platform.Phone ?
              new ToneDeltaPair(
                  ErrorContainer(), Error(), 5, TonePolarity.RelativeLighter, true,
                  DeltaConstraint.Farther) :
              null
    );
        return DynamicColor.ExtendSpecVersion(base.Error(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor ErrorDim()
    {
        return DynamicColor.FromPalette(
          name: "error_dim",
          palette: (s) => s.ErrorPalette,
          tone: (s) => TMinC(s.ErrorPalette),
          isBackground: true,
          background: (s) => SurfaceContainerHigh(),
          contrastCurve: (s) => GetCurve(4.5),
          toneDeltaPair: (s) => new ToneDeltaPair(
              ErrorDim(), Error(), 5, TonePolarity.Darker, true, DeltaConstraint.Farther)
    );
    }

    public override DynamicColor OnError()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "on_error",
          palette: (s) => s.ErrorPalette,
          background: (s) =>
              s.Platform == Platform.Phone ? Error() : ErrorDim(),
          contrastCurve: (s) => s.Platform == Platform.Phone ? GetCurve(6) : GetCurve(7)
    );
        return DynamicColor.ExtendSpecVersion(base.OnError(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor ErrorContainer()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "error_container",
          palette: (s) => s.ErrorPalette,
          tone: (s) =>
          {
              if (s.Platform == Platform.Watch)
              {
                  return 30;
              }
              else
              {
                  return s.IsDark ? TMinC(s.ErrorPalette, 30, 93) :
                                TMaxC(s.ErrorPalette, 0, 90);
              }
          },
          isBackground: true,
          background: (s) =>
              s.Platform == Platform.Phone ? HighestSurface(s) : null,
          toneDeltaPair: (s) => s.Platform == Platform.Watch ?
              new ToneDeltaPair(
                  ErrorContainer(), ErrorDim(), 10, TonePolarity.Darker, true,
                  DeltaConstraint.Farther) :
              null,
          contrastCurve: (s) => s.Platform == Platform.Phone && s.ContrastLevel > 0 ?
              GetCurve(1.5) :
              null
    );
        return DynamicColor.ExtendSpecVersion(base.ErrorContainer(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor OnErrorContainer()
    {
        var color2025 = DynamicColor.FromPalette(
          name: "on_error_container",
          palette: (s) => s.ErrorPalette,
          background: (s) => ErrorContainer(),
          contrastCurve: (s) =>
              s.Platform == Platform.Phone ? GetCurve(4.5) : GetCurve(7)
    );
        return DynamicColor.ExtendSpecVersion(base.OnErrorContainer(), SpecVersion.Spec2025, color2025);
    }

    /////////////////////////////////////////////////////////////////
    // Remapped Colors                                             //
    /////////////////////////////////////////////////////////////////

    public override DynamicColor SurfaceVariant()
    {
        var color2025 = SurfaceContainerHighest().With(name: "surface_variant");
        return DynamicColor.ExtendSpecVersion(base.SurfaceVariant(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor SurfaceTint()
    {
        var color2025 =
            Primary().With(name: "surface_tint");
        return DynamicColor.ExtendSpecVersion(base.SurfaceTint(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor Background()
    {
        var color2025 =
            Surface().With(name: "background");
        return DynamicColor.ExtendSpecVersion(base.Background(), SpecVersion.Spec2025, color2025);
    }

    public override DynamicColor OnBackground()
    {
        var color2025 = OnSurface().With(name: "on_background",
          tone: (s) =>
          {
              return s.Platform == Platform.Watch ? 100.0 : OnSurface().GetTone(s);
          });
        return DynamicColor.ExtendSpecVersion(base.OnBackground(), SpecVersion.Spec2025, color2025);
    }
}
