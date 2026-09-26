using System.Collections.Generic;
using System.Drawing;

namespace Radiant.UI.Core;

/// <summary>A node of the accessibility tree built by <see cref="UIRoot.GetSemantics"/>.</summary>
public sealed class SemanticsNode
{
    internal SemanticsNode(int id, Semantics semantics, string? label, RectangleF bounds, bool focusable, bool focused, IReadOnlyList<SemanticsNode> children)
    {
        Id = id;
        Semantics = semantics;
        Label = label;
        Bounds = bounds;
        IsFocusable = focusable;
        IsFocused = focused;
        Children = children;
    }

    /// <summary>
    /// Which node this is, the same from one tree to the next for as long as the node it describes
    /// lives: for assistive technology to keep track of it, and to act on it
    /// (<see cref="UIRoot.Press"/>, <see cref="UIRoot.FocusNode"/>). The root is 0.
    /// </summary>
    public int Id { get; }

    /// <summary>Role and state.</summary>
    public Semantics Semantics { get; }

    /// <summary>The role.</summary>
    public SemanticsRole Role => Semantics.Role;

    /// <summary>The name: the given label, or the text inside.</summary>
    public string? Label { get; }

    /// <summary>Where it is, in the root's coordinates.</summary>
    public RectangleF Bounds { get; }

    /// <summary>Whether it can take keyboard focus.</summary>
    public bool IsFocusable { get; }

    /// <summary>Whether it has keyboard focus.</summary>
    public bool IsFocused { get; }

    /// <summary>What it contains.</summary>
    public IReadOnlyList<SemanticsNode> Children { get; }

    /// <summary>For debugging: role, label and children, indented.</summary>
    public override string ToString() => ToString(0);

    private string ToString(int depth)
    {
        var text = $"{new string(' ', depth * 2)}{Role}{(Label is null ? "" : $" \"{Label}\"")}";
        foreach (var child in Children)
        {
            text += "\n" + child.ToString(depth + 1);
        }
        return text;
    }
}
