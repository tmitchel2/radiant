using System;
using Radiant.Components.Primitives;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A floating panel of any content beside an anchor (a filter panel under a button, details beside
/// an item). It takes focus when it opens and closes on Escape or a press outside it, giving focus
/// back. Unlike a <see cref="Dialog"/> it doesn't dim the app, and Tab can leave it unless it's
/// <see cref="Modal"/>.
/// </summary>
/// <param name="Anchor">What it opens beside.</param>
/// <param name="Open">Whether it's showing.</param>
/// <param name="OnClose">Called when it should close.</param>
/// <param name="Content">What it shows.</param>
public sealed record Popover(ElementRef Anchor, bool Open, Action OnClose, Element? Content) : Component
{
    /// <summary>Which side of the anchor it opens on (it flips if there's no room).</summary>
    public Side Side { get; init; } = Side.Bottom;

    /// <summary>How it lines up with the anchor.</summary>
    public SideAlign Align { get; init; } = SideAlign.Center;

    /// <summary>Whether Tab stays inside it.</summary>
    public bool Modal { get; init; }

    /// <summary>What assistive technology calls it.</summary>
    public string? Label { get; init; }

    /// <summary>The panel's size (it fits its content by default, up to 360 wide).</summary>
    public LayoutStyle? Layout { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var (anchor, close, content, side, align, modal, label, layout) = (Anchor, OnClose, Content, Side, Align, Modal, Label, Layout);
        return new Presence(Open, progress => new Anchored(anchor, new DismissableLayer(new FocusScope(new Surface
        {
            SurfaceColor = SurfaceName.SurfaceContainerHigh,
            CornerShape = CornerShapeRole.Medium,
            Elevation = ElevationLevel.Level2,
            Semantics = new Semantics { Role = SemanticsRole.Dialog, Label = label },
            Layout = new LayoutStyle { MaxWidth = 360, Padding = Edges.All(16) }.Merge(layout ?? default),
            Children = [new Box { Opacity = progress, Children = [content] }],
        })
        { Trap = modal }, close)) { Side = side, Align = align, Offset = 8f })
        { Duration = TimeSpan.FromMilliseconds(150) };
    }
}
