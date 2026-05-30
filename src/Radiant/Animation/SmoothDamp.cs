using System;
using System.Numerics;

namespace Radiant.Animation;

/// <summary>
/// Critically-damped spring smoothing — the same algorithm used by Unity's
/// <c>Mathf.SmoothDamp</c> and yomotsu/camera-controls. Gradually moves a value
/// toward a target without overshoot, carrying a caller-owned velocity between
/// frames. Frame-rate independent.
///
/// This is the single source of truth for spring smoothing across Radiant and
/// its consumers (scroll settle/animated-scroll, and the Dynamis orbit camera,
/// which forwards here).
/// </summary>
public static class SmoothDamp
{
    /// <summary>
    /// Advance <paramref name="current"/> toward <paramref name="target"/> by one
    /// <paramref name="deltaTime"/> step, mutating <paramref name="velocity"/>.
    /// </summary>
    /// <param name="current">The current value.</param>
    /// <param name="target">The value to approach.</param>
    /// <param name="velocity">Caller-owned velocity state; persists across calls.</param>
    /// <param name="smoothTime">Approximate time to reach the target. Smaller is snappier.</param>
    /// <param name="maxSpeed">Maximum speed clamp.</param>
    /// <param name="deltaTime">Elapsed time for this step, in seconds.</param>
    /// <returns>The new value after this step.</returns>
    public static float Step(
        float current, float target, ref float velocity,
        float smoothTime, float maxSpeed, float deltaTime)
    {
        smoothTime = MathF.Max(0.0001f, smoothTime);
        var omega = 2f / smoothTime;
        var x = omega * deltaTime;
        var exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);

        var change = current - target;
        var originalTo = target;

        var maxChange = maxSpeed * smoothTime;
        change = Math.Clamp(change, -maxChange, maxChange);
        target = current - change;

        var temp = (velocity + omega * change) * deltaTime;
        velocity = (velocity - omega * temp) * exp;
        var output = target + (change + temp) * exp;

        // Prevent overshooting.
        if ((originalTo - current > 0f) == (output > originalTo))
        {
            output = originalTo;
            velocity = (output - originalTo) / deltaTime;
        }

        return output;
    }

    /// <summary>
    /// <see cref="Step(float, float, ref float, float, float, float)"/> with no speed clamp.
    /// </summary>
    public static float Step(
        float current, float target, ref float velocity,
        float smoothTime, float deltaTime) =>
        Step(current, target, ref velocity, smoothTime, float.PositiveInfinity, deltaTime);

    /// <summary>Per-component <see cref="Step(float, float, ref float, float, float, float)"/> for 2D values.</summary>
    public static Vector2 Step(
        Vector2 current, Vector2 target, ref Vector2 velocity,
        float smoothTime, float maxSpeed, float deltaTime) =>
        new(
            Step(current.X, target.X, ref velocity.X, smoothTime, maxSpeed, deltaTime),
            Step(current.Y, target.Y, ref velocity.Y, smoothTime, maxSpeed, deltaTime));

    /// <summary>Per-component <see cref="Step(float, float, ref float, float, float, float)"/> for 3D values.</summary>
    public static Vector3 Step(
        Vector3 current, Vector3 target, ref Vector3 velocity,
        float smoothTime, float maxSpeed, float deltaTime) =>
        new(
            Step(current.X, target.X, ref velocity.X, smoothTime, maxSpeed, deltaTime),
            Step(current.Y, target.Y, ref velocity.Y, smoothTime, maxSpeed, deltaTime),
            Step(current.Z, target.Z, ref velocity.Z, smoothTime, maxSpeed, deltaTime));
}
