using System.Collections.Generic;
using System.Numerics;
using Radiant.Layout;
using Radiant.Scrolling;

namespace Radiant.UI.Core;

/// <summary>
/// A viewport onto content that may be larger than it, scrolled by the wheel or trackpad with
/// Radiant's scroll physics (momentum, bounce, snapping per <see cref="Behaviour"/>). Content is
/// laid out unconstrained along the scrolling axes. Scroll position survives rebuilds; pass a
/// <see cref="Controller"/> to read or set it from outside.
/// </summary>
public sealed record ScrollArea : HostElement
{
    /// <summary>The viewport's size and placement.</summary>
    public LayoutStyle Layout { get; init; }

    /// <summary>How the content is laid out: a column by default, as wide as the viewport when scrolling vertically.</summary>
    public LayoutStyle ContentLayout { get; init; }

    /// <summary>Which axes scroll, and how it feels.</summary>
    public ScrollBehaviour Behaviour { get; init; } = new();

    /// <summary>A controller to scroll from outside or read the position; the area makes its own if null.</summary>
    public ScrollController? Controller { get; init; }

    /// <summary>The colour of the scroll indicators; transparent to hide them.</summary>
    public Vector4 IndicatorColor { get; init; } = new(0f, 0f, 0f, 0.35f);

    /// <summary>What assistive technology is told about the area; a <see cref="SemanticsRole.ScrollArea"/> if null.</summary>
    public Semantics? Semantics { get; init; }

    /// <summary>The content.</summary>
    public IReadOnlyList<Element?> Children { get; init; } = [];

    internal override RenderNode CreateRenderNode() => new ScrollRenderNode();

    internal override IReadOnlyList<Element?> ChildElements => Children;
}
