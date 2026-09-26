using System.Collections.Generic;
using System.Numerics;

namespace Radiant.UI.Core;

/// <summary>A pointer press seen by <see cref="UIRoot.ObservePointerDown"/>: where it was and what it hit.</summary>
public sealed class PointerDownObservation
{
    private readonly List<RenderNode> _path;

    internal PointerDownObservation(Vector2 position, PointerButton button, List<RenderNode> path)
    {
        Position = position;
        Button = button;
        _path = path;
    }

    /// <summary>Where the press was, in root coordinates.</summary>
    public Vector2 Position { get; }

    /// <summary>The button pressed.</summary>
    public PointerButton Button { get; }

    /// <summary>
    /// Whether the press landed in <paramref name="box"/>'s box or anything inside it (by element
    /// tree, so content it shows in a portal counts as inside).
    /// </summary>
    public bool IsWithin(ElementRef box)
    {
        System.ArgumentNullException.ThrowIfNull(box);
        return box.Node is { } node && _path.Contains(node);
    }
}
