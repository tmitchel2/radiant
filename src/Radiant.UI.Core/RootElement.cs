using System.Collections.Generic;

namespace Radiant.UI.Core;

/// <summary>The top of every tree: the viewport, holding the app's element.</summary>
internal sealed record RootElement(Element? Child) : HostElement
{
    internal override RenderNode CreateRenderNode() => new RootRenderNode();

    internal override IReadOnlyList<Element?> ChildElements => [Child];
}
