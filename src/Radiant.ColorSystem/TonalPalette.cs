// Copyright 2021 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

using System;
using System.Collections.Concurrent;

namespace Radiant.ColorSystem;

/// <summary>
/// A convenience class for retrieving colors that are constant in hue and chroma, but vary in tone.
/// <para>
/// Tones are cached as they are computed; the cache is safe to use from several threads at once.
/// </para>
/// </summary>
public sealed class TonalPalette
{
    private readonly ConcurrentDictionary<double, int> _cache = new();

    private TonalPalette(double hue, double chroma, Hct keyColor)
    {
        Hue = hue;
        Chroma = chroma;
        KeyColor = keyColor;
    }

    /// <summary>The HCT hue shared by every tone in the palette.</summary>
    public double Hue { get; }

    /// <summary>The HCT chroma shared by every tone in the palette (the most each tone may have).</summary>
    public double Chroma { get; }

    /// <summary>The color that represents the palette's hue and chroma.</summary>
    public Hct KeyColor { get; }

    /// <summary>Tones matching the hue and chroma of an ARGB color.</summary>
    public static TonalPalette FromInt(int argb)
    {
        var hct = Hct.FromInt(argb);
        return FromHct(hct);
    }

    /// <summary>Tones matching the hue and chroma of an HCT color.</summary>
    public static TonalPalette FromHct(Hct hct)
    {
        ArgumentNullException.ThrowIfNull(hct);
        return new TonalPalette(hct.Hue, hct.Chroma, hct);
    }

    /// <summary>Tones matching an HCT hue and chroma.</summary>
    public static TonalPalette FromHueAndChroma(double hue, double chroma)
    {
        var keyColor = new KeyColor(hue, chroma).Create();
        return new TonalPalette(hue, chroma, keyColor);
    }

    /// <summary>The ARGB representation of the palette's color with a tone, measured from 0 to 100.</summary>
    public int Tone(double tone) => _cache.GetOrAdd(tone, ComputeTone);

    /// <summary>The HCT representation of the palette's color with a tone.</summary>
    public Hct GetHct(double tone) => Hct.FromInt(Tone(tone));

    private int ComputeTone(double tone)
    {
        if (tone == 99 && Hct.IsYellow(Hue))
        {
            return AverageArgb(Tone(98), Tone(100));
        }
        else
        {
            return Hct.From(Hue, Chroma, tone).ToInt();
        }
    }

    private static int AverageArgb(int argb1, int argb2)
    {
        var red1 = (argb1 >> 16) & 0xff;
        var green1 = (argb1 >> 8) & 0xff;
        var blue1 = argb1 & 0xff;
        var red2 = (argb2 >> 16) & 0xff;
        var green2 = (argb2 >> 8) & 0xff;
        var blue2 = argb2 & 0xff;
        var red = (int)MathUtils.Round((red1 + red2) / 2.0);
        var green = (int)MathUtils.Round((green1 + green2) / 2.0);
        var blue = (int)MathUtils.Round((blue1 + blue2) / 2.0);
        return (255 << 24) | ((red & 255) << 16) | ((green & 255) << 8) | (blue & 255);
    }
}
