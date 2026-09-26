using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>A panel a <see cref="DockPanel"/> can show: its content and the tab it's known by.</summary>
/// <param name="Id">What identifies it in a <see cref="DockLayout"/>.</param>
/// <param name="Title">What its tab says.</param>
/// <param name="Content">What it shows.</param>
public sealed record DockItem(string Id, string Title, Element? Content)
{
    /// <summary>An icon before the title.</summary>
    public string? Icon { get; init; }

    /// <summary>Whether it can be closed (the default); closing takes it out of the layout.</summary>
    public bool Closable { get; init; } = true;
}
