using Facebook.Yoga;
using static Facebook.Yoga.YGNodeAPI;
using static Facebook.Yoga.YGNodeLayoutAPI;
using static Facebook.Yoga.YGNodeStyleAPI;

namespace Radiant.UI.Core;

/// <summary>
/// Lays out a <see cref="Grid"/> as a wrapping row whose children are given the column width.
/// The column width depends on the grid's own laid-out width, so it is worked out after layout
/// (<see cref="Resolve"/>) and, when it changed, the root lays out again; until the width
/// changes, each layout reuses the last column width.
/// </summary>
internal sealed class GridRenderNode : RenderNode
{
    // Floating-point sums of the cells and gaps can land a hair over the width and wrap the last
    // column; cells are this much narrower.
    private const float Slack = 0.01f;

    private float _cellWidth = float.NaN;

    public Grid Element { get; private set; } = null!;

    public override bool IsHitTestVisible => false;

    public override void Update(HostElement element, HostElement? previous)
    {
        Element = (Grid)element;
        var old = previous as Grid;
        if (old is null)
        {
            Owner.Root.AddGrid(this);
        }
        if (old is null || old.Layout != Element.Layout || old.ColumnGap != Element.ColumnGap || old.RowGap != Element.RowGap)
        {
            YogaStyle.Set(Yoga, Element.Layout, reset: old is not null);
            YGNodeStyleSetFlexDirection(Yoga, YGFlexDirection.Row);
            YGNodeStyleSetFlexWrap(Yoga, YGWrap.Wrap);
            YGNodeStyleSetGap(Yoga, YGGutter.Column, Element.ColumnGap);
            YGNodeStyleSetGap(Yoga, YGGutter.Row, Element.RowGap);
        }
    }

    /// <summary>Gives every child the current column width (a rebuilt child's style loses it).</summary>
    public void ApplyCellWidth()
    {
        if (float.IsNaN(_cellWidth))
        {
            return;
        }
        foreach (var child in Children)
        {
            // Yoga ignores a set to the value already there, so this dirties only what changed.
            YGNodeStyleSetWidth(child.Yoga, _cellWidth);
            YGNodeStyleSetFlexGrow(child.Yoga, 0f);
            YGNodeStyleSetFlexShrink(child.Yoga, 0f);
            YGNodeStyleSetFlexBasisAuto(child.Yoga);
        }
    }

    /// <summary>
    /// Works out the column width from the laid-out width and applies it; true when that changed
    /// the layout, so it must be redone.
    /// </summary>
    public bool Resolve()
    {
        var width = Size.X
            - YGNodeLayoutGetPadding(Yoga, YGEdge.Left) - YGNodeLayoutGetPadding(Yoga, YGEdge.Right)
            - YGNodeLayoutGetBorder(Yoga, YGEdge.Left) - YGNodeLayoutGetBorder(Yoga, YGEdge.Right);
        if (width <= 0f || float.IsNaN(width))
        {
            return false;
        }
        var columns = Element.ColumnCount(width);
        var cell = System.MathF.Max(0f, (width - Element.ColumnGap * (columns - 1)) / columns - Slack);
        if (cell != _cellWidth)
        {
            _cellWidth = cell;
        }
        ApplyCellWidth();
        return YGNodeIsDirty(Yoga);
    }

    public override void Dispose()
    {
        Owner.Root.RemoveGrid(this);
        base.Dispose();
    }
}
