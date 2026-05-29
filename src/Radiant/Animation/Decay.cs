using System;

namespace Radiant.Animation;

/// <summary>
/// Frame-rate-independent exponential velocity decay — the momentum/inertia model
/// used by native scroll views. Velocity decays as <c>v(t) = v0 · rate^(t_ms)</c>,
/// where <c>rate</c> is a per-millisecond retention factor (e.g. iOS "normal"
/// ≈ 0.998/ms, "fast" ≈ 0.99/ms).
///
/// All helpers are pure and dt-invariant: decaying once over a 32 ms step yields
/// the same velocity as decaying twice over 16 ms steps. Never decay with a naive
/// per-frame <c>v *= 0.99</c> — that is frame-rate dependent.
/// </summary>
public static class Decay
{
    /// <summary>iOS "normal" deceleration: retains 99.8% of velocity per millisecond.</summary>
    public const float NormalRatePerMs = 0.998f;

    /// <summary>iOS "fast" deceleration: retains 99% of velocity per millisecond.</summary>
    public const float FastRatePerMs = 0.99f;

    /// <summary>The multiplicative velocity-retention factor for an elapsed <paramref name="deltaTime"/> (seconds).</summary>
    public static float Factor(float ratePerMs, double deltaTime) =>
        MathF.Pow(ratePerMs, (float)(deltaTime * 1000.0));

    /// <summary>
    /// Decay <paramref name="velocity"/> over <paramref name="deltaTime"/> seconds and return it.
    /// </summary>
    public static float Apply(ref float velocity, float ratePerMs, double deltaTime)
    {
        velocity *= Factor(ratePerMs, deltaTime);
        return velocity;
    }

    /// <summary>
    /// Continuous decay constant λ (per second) equivalent to <paramref name="ratePerMs"/>,
    /// such that <c>v(t) = v0 · e^(−λ·t)</c>. Larger λ = faster stop.
    /// </summary>
    public static float Lambda(float ratePerMs) =>
        -1000f * MathF.Log(ratePerMs);

    /// <summary>
    /// The total additional distance a body of the given <paramref name="velocity"/> (units/sec)
    /// will travel before coming to rest under decay <paramref name="ratePerMs"/>. This is the
    /// analytic integral <c>∫₀^∞ v0·e^(−λt) dt = v0/λ</c> — use it to predict the resting
    /// offset for snap/paging instead of iterating frames.
    /// </summary>
    public static float ProjectedDistance(float velocity, float ratePerMs) =>
        velocity / Lambda(ratePerMs);
}
