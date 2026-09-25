namespace Radiant.Host;

/// <summary>
/// How a renderer process presents itself as a tab. See <see cref="TabSession"/>.
/// </summary>
public sealed class TabAttachOptions
{
    /// <summary>The instance name this tab registers and is addressed under. Required.</summary>
    public string InstanceName { get; set; } = "";

    /// <summary>Logical content width the tab starts at, before the host says otherwise.</summary>
    public int Width { get; set; } = 1280;

    /// <summary>Logical content height the tab starts at.</summary>
    public int Height { get; set; } = 720;

    /// <summary>
    /// The largest physical frame the shared buffer is sized for. The mapping is allocated once at
    /// this size (three slots of MaxWidth * MaxHeight * 4), and every published frame carries its
    /// own dimensions — so the host can resize the tab without either side reallocating.
    /// </summary>
    public int MaxWidth { get; set; } = 3840;

    /// <summary>The largest physical frame height.</summary>
    public int MaxHeight { get; set; } = 2160;

    /// <summary>
    /// What this instance advertises it can do, for `instance actions`-style discovery.
    /// <para>
    /// Must NOT contain "tab". That capability marks a compositing HOST, and an auto-attaching
    /// launcher decides whether to spawn one by asking whether any instance advertises it — so a
    /// renderer claiming it would suppress the host it is waiting for.
    /// </para>
    /// </summary>
    public string[] Capabilities { get; set; } = [];

    /// <summary>Where this instance keeps its own state, recorded in <c>instance.json</c>.</summary>
    public string StateDirectory { get; set; } = "";
}
