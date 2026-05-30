namespace Radiant.Animation;

/// <summary>
/// Implemented by anything that may need the host to keep rendering frames even
/// when no input arrives this frame (momentum, spring-settle, animated scroll).
/// The host ORs <see cref="IsAnimating"/> across the UI tree into its
/// idle-throttle decision, mirroring the camera's <c>IsAtRest</c> pattern.
/// </summary>
public interface IAnimating
{
    /// <summary>True while time-driven motion is in progress and the element must be ticked next frame.</summary>
    bool IsAnimating { get; }
}
