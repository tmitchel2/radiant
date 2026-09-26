using System.Collections.Generic;

namespace Radiant.UI.Core;

/// <summary>
/// An element the framework lays out and draws itself, through a render node it keeps for the
/// element's lifetime. Components are made of these; only Radiant defines them.
/// </summary>
public abstract record HostElement : Element
{
    /// <summary>Makes the render node when the element first appears.</summary>
    internal abstract RenderNode CreateRenderNode();

    /// <summary>The child elements, laid out inside this one.</summary>
    internal virtual IReadOnlyList<Element?> ChildElements => [];
}
