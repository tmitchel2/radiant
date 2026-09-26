using System;
using System.Numerics;
using Radiant.Graphics2D;

namespace Radiant.Theming;

/// <summary>
/// Björn Ottosson's OKLab: a perceptual colour space in which straight-line mixes look even, so a
/// theme transition from blue to orange passes through neither grey nor an odd hue.
/// </summary>
internal static class Oklab
{
    /// <summary>Mixes two colours in OKLab (alpha linearly).</summary>
    public static Color Lerp(Color from, Color to, float t)
    {
        var a = ToOklab(from);
        var b = ToOklab(to);
        var mixed = FromOklab(Vector3.Lerp(a, b, t));
        return new Color(mixed.X, mixed.Y, mixed.Z, from.A + (to.A - from.A) * t);
    }

    public static Vector3 ToOklab(Color c)
    {
        var l = MathF.Cbrt(0.4122214708f * c.R + 0.5363325363f * c.G + 0.0514459929f * c.B);
        var m = MathF.Cbrt(0.2119034982f * c.R + 0.6806995451f * c.G + 0.1073969566f * c.B);
        var s = MathF.Cbrt(0.0883024619f * c.R + 0.2817188376f * c.G + 0.6299787005f * c.B);
        return new Vector3(
            0.2104542553f * l + 0.7936177850f * m - 0.0040720468f * s,
            1.9779984951f * l - 2.4285922050f * m + 0.4505937099f * s,
            0.0259040371f * l + 0.7827717662f * m - 0.8086757660f * s);
    }

    public static Vector3 FromOklab(Vector3 lab)
    {
        var l = lab.X + 0.3963377774f * lab.Y + 0.2158037573f * lab.Z;
        var m = lab.X - 0.1055613458f * lab.Y - 0.0638541728f * lab.Z;
        var s = lab.X - 0.0894841775f * lab.Y - 1.2914855480f * lab.Z;
        l = l * l * l;
        m = m * m * m;
        s = s * s * s;
        return new Vector3(
            4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s,
            -1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s,
            -0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s);
    }
}
