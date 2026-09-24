namespace Radiant.Host.Ipc;

/// <summary>
/// Version stamp for the multi-process tab protocol — the contract a headless <c>--attach</c>
/// renderer and the compositing host agree on (shared-memory frame buffer layout, input ring layout,
/// and the host-side <c>tab.*</c> control actions).
///
/// <para>A renderer advertises the version it speaks via <c>InstanceInfo.ProtocolVersion</c> when it
/// registers. The host compares that against <see cref="Version"/> before adopting the renderer as a
/// tab, so an older worktree build that can't speak the current protocol is rejected with a clear
/// message instead of having its frame buffer opened (where a layout drift could surface as a torn
/// image or an unexpected exception). The shared-memory transports additionally carry their own
/// magic + version words as a second, lower-level guard.</para>
///
/// <para>Bump this whenever the wire layout of <see cref="Frames.SharedFrameBuffer"/>,
/// <see cref="Input.InputRing"/>, or the <c>tab.*</c> action contract changes incompatibly.</para>
/// </summary>
public static class TabProtocol
{
    /// <summary>Current multi-process tab protocol version.</summary>
    /// <remarks>
    /// v2: added the input-ring <c>Active</c> field (renderer-side background pause). An older
    /// renderer that predates it is not adopted as a tab, so the "old host writes nothing / new
    /// renderer reads zero → paused forever" hazard cannot arise across mismatched builds.
    /// <para>v3: the transports moved from Dynamis into Radiant and their magic words changed with them
    /// (frame buffer <c>DYN1</c> → <c>RAD1</c>, input ring <c>DIN1</c> → <c>RIN1</c>). No layout changed;
    /// the bump keeps a pre-move build from being adopted and then failing on the magic check.</para>
    /// </remarks>
    public const int Version = 3;
}
