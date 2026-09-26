// Part of the C# port of material-color-utilities (see README.md). Not upstream code: reproduces
// java.util.Random, as its documentation specifies the algorithm, because upstream's Java quantizer
// draws from it.

using System;

namespace Radiant.MaterialColor;

/// <summary>
/// Java's <c>java.util.Random</c>: a 48-bit linear congruential generator, reproduced exactly so
/// that <see cref="QuantizerWsmeans"/> seeds its clusters as upstream's Java port does. Only what
/// that port calls is here.
/// </summary>
internal sealed class JavaRandom
{
    private const long Multiplier = 0x5DEECE66DL;
    private const long Addend = 0xBL;
    private const long Mask = (1L << 48) - 1;

    private long _seed;

    /// <summary>A generator seeded as <c>new java.util.Random(seed)</c> is.</summary>
    public JavaRandom(long seed)
    {
        SetSeed(seed);
    }

    /// <summary>Reseeds the generator, scrambling the seed as <c>Random.setSeed</c> does.</summary>
    public void SetSeed(long seed)
    {
        _seed = (seed ^ Multiplier) & Mask;
    }

    /// <summary>The next pseudorandom <see cref="int"/>, all 2^32 values equally likely.</summary>
    public int NextInt() => Next(32);

    /// <summary>
    /// The next pseudorandom <see cref="int"/> from 0 (inclusive) to <paramref name="bound"/>
    /// (exclusive), by <c>Random.nextInt(int)</c>'s algorithm: a scaled draw when the bound is a
    /// power of two, otherwise a remainder, rejecting draws that would bias it.
    /// </summary>
    public int NextInt(int bound)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bound);
        var r = Next(31);
        var m = bound - 1;
        if ((bound & m) == 0)
        {
            r = (int)((bound * (long)r) >> 31);
        }
        else
        {
            // Java's int arithmetic wraps: u - r + m goes negative exactly when u is in the final,
            // partial run of bound values, which would favour small results.
            var u = r;
            while (unchecked(u - (r = u % bound) + m) < 0)
            {
                u = Next(31);
            }
        }
        return r;
    }

    private int Next(int bits)
    {
        _seed = unchecked(_seed * Multiplier + Addend) & Mask;
        return unchecked((int)(_seed >>> (48 - bits)));
    }
}
