using System.Collections.Generic;

namespace Radiant.UI.Core;

/// <summary>
/// The live counterpart of an element: where its state, children and (for host elements) render
/// node persist between builds. Each build's elements are matched to these by type, key and
/// position, and the matched node takes the new element.
/// </summary>
internal sealed class ElementNode
{
    public ElementNode(UIRoot root, ElementNode? parent, Element element, int slot)
    {
        Root = root;
        Parent = parent;
        Element = element;
        Slot = slot;
        Depth = parent is null ? 0 : parent.Depth + 1;
    }

    public UIRoot Root { get; }

    public ElementNode? Parent { get; }

    /// <summary>The element from the latest build.</summary>
    public Element Element { get; set; }

    /// <summary>The position among the parent's child elements (nulls included), which matches unkeyed elements.</summary>
    public int Slot { get; set; }

    /// <summary>How deep in the tree, the root being 0: parents rebuild before their children.</summary>
    public int Depth { get; }

    public List<ElementNode> Children { get; set; } = [];

    /// <summary>For host elements, what is laid out and drawn.</summary>
    public RenderNode? RenderNode { get; set; }

    /// <summary>For components, the hooks and the context handed to Build.</summary>
    public BuildContext? Context { get; set; }

    /// <summary>For providers, the nodes that read the value and are rebuilt when it changes.</summary>
    public HashSet<ElementNode>? Consumers { get; set; }

    /// <summary>Whether state this node uses has changed since it was last built.</summary>
    public bool Dirty { get; set; }

    public bool Mounted { get; set; }

    /// <summary>The nearest node, this one or above, with a render node.</summary>
    public ElementNode NearestHost()
    {
        var node = this;
        while (node.RenderNode is null)
        {
            node = node.Parent!;
        }
        return node;
    }

    public override string ToString() => $"{Element.GetType().Name}{(Element.Key is { } key ? $" [{key}]" : "")}";
}
