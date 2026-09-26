using System.Collections.Generic;
using Radiant.Layout;

namespace Radiant.UI.Core;

/// <summary>
/// Shows its children above everything else, in the root's coordinates, wherever the portal is
/// in the tree: how menus, popovers, dialogs and tooltips escape their parents' clips and layout.
/// The children still belong to the component that renders the portal: its state, contexts and
/// events bubble through it as usual (to its element ancestors, not its visual ones).
/// Later portals are above earlier ones.
/// </summary>
public sealed record Portal : HostElement
{
    /// <summary>A portal showing <paramref name="children"/>.</summary>
    public Portal(params Element?[] children) => Children = children;

    /// <summary>The layer's layout: the whole viewport by default, so children can position themselves in it.</summary>
    public LayoutStyle Layout { get; init; } = new()
    {
        Position = PositionType.Absolute,
        Inset = Edges.All(0),
    };

    /// <summary>What to show above everything.</summary>
    public IReadOnlyList<Element?> Children { get; init; }

    internal override RenderNode CreateRenderNode() => new PortalRenderNode();

    internal override IReadOnlyList<Element?> ChildElements => Children;
}
