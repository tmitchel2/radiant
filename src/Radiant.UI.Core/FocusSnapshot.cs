namespace Radiant.UI.Core;

/// <summary>What had focus at a moment, from <see cref="UIRoot.SaveFocus"/>, to give it back later.</summary>
public sealed class FocusSnapshot
{
    private readonly UIRoot _root;
    private readonly RenderNode? _node;
    private readonly bool _visible;

    internal FocusSnapshot(UIRoot root, RenderNode? node, bool visible)
    {
        _root = root;
        _node = node;
        _visible = visible;
    }

    /// <summary>Gives focus back, if what had it is still there.</summary>
    public void Restore() => _root.RestoreFocus(_node, _visible);
}
