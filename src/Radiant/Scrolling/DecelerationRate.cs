using Radiant.Animation;

namespace Radiant.Scrolling;

/// <summary>
/// Momentum deceleration, expressed as a per-millisecond velocity-retention factor
/// (the React Native / iOS model). Use <see cref="Normal"/> or <see cref="Fast"/>,
/// or construct a custom rate.
/// </summary>
public readonly record struct DecelerationRate(float RatePerMs)
{
    /// <summary>iOS "normal" deceleration (long glide). ≈ 0.998 / ms.</summary>
    public static DecelerationRate Normal => new(Decay.NormalRatePerMs);

    /// <summary>iOS "fast" deceleration (quick stop). ≈ 0.99 / ms.</summary>
    public static DecelerationRate Fast => new(Decay.FastRatePerMs);
}
