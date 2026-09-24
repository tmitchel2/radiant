namespace Radiant.Host.Ipc.Frames;

/// <summary>
/// Pixel layout of a frame published through a <see cref="SharedFrameBuffer"/>.
/// Values are stable on the wire (stored in the shared-memory header) so a host and a
/// renderer built from different worktrees agree on interpretation.
/// </summary>
public enum FrameFormat
{
    /// <summary>8 bits per channel, R,G,B,A byte order. Matches <c>TextureFormat.Rgba8Unorm</c>.</summary>
    Rgba8Unorm = 0,

    /// <summary>8 bits per channel, B,G,R,A byte order. Matches <c>TextureFormat.Bgra8Unorm</c> (the swapchain format).</summary>
    Bgra8Unorm = 1,
}
