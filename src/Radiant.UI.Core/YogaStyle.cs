using System;
using Facebook.Yoga;
using Radiant.Layout;
using static Facebook.Yoga.YGNodeStyleAPI;
using Align = Radiant.Layout.Align;
using Dimension = Radiant.Layout.Dimension;
using FlexDirection = Radiant.Layout.FlexDirection;
using Justify = Radiant.Layout.Justify;
using PositionType = Radiant.Layout.PositionType;

namespace Radiant.UI.Core;

/// <summary>
/// Maps a <see cref="LayoutStyle"/> onto a Yoga node. Render nodes keep their Yoga node for their
/// whole life, so a style that changes is first reset to Yoga's defaults: a property that was set
/// and is now unset must go back to its default, not keep its old value.
/// </summary>
internal static class YogaStyle
{
    /// <summary>
    /// Sets <paramref name="owner"/>'s node (or another node it holds) to exactly
    /// <paramref name="style"/>. A node that hugs (<see cref="Align.Hug"/>) is registered with the
    /// root, which resolves it from its parent's direction before each layout.
    /// </summary>
    public static void Set(RenderNode owner, Node node, in LayoutStyle style, bool reset)
    {
        if (reset)
        {
            Reset(node);
        }
        Apply(node, style);
        owner.Owner.Root.TrackHug(node, style.AlignSelf == Align.Hug);
    }

    private static void Reset(Node node)
    {
        YGNodeStyleSetFlexDirection(node, YGFlexDirection.Column);
        YGNodeStyleSetJustifyContent(node, YGJustify.FlexStart);
        YGNodeStyleSetAlignItems(node, YGAlign.Stretch);
        YGNodeStyleSetAlignSelf(node, YGAlign.Auto);
        YGNodeStyleSetFlexWrap(node, YGWrap.NoWrap);
        YGNodeStyleSetPositionType(node, YGPositionType.Relative);
        YGNodeStyleSetFlexGrow(node, 0f);
        YGNodeStyleSetFlexShrink(node, 0f);
        YGNodeStyleSetAspectRatio(node, float.NaN);
        YGNodeStyleSetDirection(node, YGDirection.Inherit);
        YGNodeStyleSetFlexBasisAuto(node);
        YGNodeStyleSetWidthAuto(node);
        YGNodeStyleSetHeightAuto(node);
        // NaN unsets a length.
        YGNodeStyleSetMinWidth(node, float.NaN);
        YGNodeStyleSetMinHeight(node, float.NaN);
        YGNodeStyleSetMaxWidth(node, float.NaN);
        YGNodeStyleSetMaxHeight(node, float.NaN);
        foreach (var edge in s_edges)
        {
            YGNodeStyleSetMargin(node, edge, float.NaN);
            YGNodeStyleSetPadding(node, edge, float.NaN);
            YGNodeStyleSetPosition(node, edge, float.NaN);
        }
        YGNodeStyleSetGap(node, YGGutter.Row, float.NaN);
        YGNodeStyleSetGap(node, YGGutter.Column, float.NaN);
    }

    private static readonly YGEdge[] s_edges = [YGEdge.Start, YGEdge.Top, YGEdge.End, YGEdge.Bottom];

    private static void Apply(Node node, in LayoutStyle s)
    {
        if (s.FlexDirection is { } flexDirection) YGNodeStyleSetFlexDirection(node, Map(flexDirection));
        if (s.JustifyContent is { } justify) YGNodeStyleSetJustifyContent(node, Map(justify));
        if (s.AlignItems is { } alignItems) YGNodeStyleSetAlignItems(node, Map(alignItems));
        if (s.AlignSelf is { } alignSelf) YGNodeStyleSetAlignSelf(node, Map(alignSelf));
        if (s.FlexWrap is { } wrap) YGNodeStyleSetFlexWrap(node, Map(wrap));
        if (s.Position is { } position) YGNodeStyleSetPositionType(node, Map(position));
        if (s.FlexGrow is { } grow) YGNodeStyleSetFlexGrow(node, grow);
        if (s.FlexShrink is { } shrink) YGNodeStyleSetFlexShrink(node, shrink);
        if (s.AspectRatio is { } aspectRatio) YGNodeStyleSetAspectRatio(node, aspectRatio);
        if (s.Direction is { } direction) YGNodeStyleSetDirection(node, direction == Radiant.Text.TextDirection.RightToLeft ? YGDirection.RTL : YGDirection.LTR);

        switch (s.FlexBasis.Unit)
        {
            case DimensionUnit.Point: YGNodeStyleSetFlexBasis(node, s.FlexBasis.Value); break;
            case DimensionUnit.Percent: YGNodeStyleSetFlexBasisPercent(node, s.FlexBasis.Value); break;
            case DimensionUnit.Auto: YGNodeStyleSetFlexBasisAuto(node); break;
            case DimensionUnit.Undefined:
            default:
                break;
        }
        Size(node, s.Width, YGNodeStyleSetWidth, YGNodeStyleSetWidthPercent, YGNodeStyleSetWidthAuto);
        Size(node, s.Height, YGNodeStyleSetHeight, YGNodeStyleSetHeightPercent, YGNodeStyleSetHeightAuto);
        Limit(node, s.MinWidth, YGNodeStyleSetMinWidth, YGNodeStyleSetMinWidthPercent);
        Limit(node, s.MinHeight, YGNodeStyleSetMinHeight, YGNodeStyleSetMinHeightPercent);
        Limit(node, s.MaxWidth, YGNodeStyleSetMaxWidth, YGNodeStyleSetMaxWidthPercent);
        Limit(node, s.MaxHeight, YGNodeStyleSetMaxHeight, YGNodeStyleSetMaxHeightPercent);
        Edges(node, s.Margin, YGNodeStyleSetMargin, YGNodeStyleSetMarginPercent, YGNodeStyleSetMarginAuto);
        Edges(node, s.Padding, YGNodeStyleSetPadding, YGNodeStyleSetPaddingPercent, setAuto: null);
        Edges(node, s.Inset, YGNodeStyleSetPosition, YGNodeStyleSetPositionPercent, setAuto: null);
        if (s.RowGap.IsSet) YGNodeStyleSetGap(node, YGGutter.Row, s.RowGap.Value);
        if (s.ColumnGap.IsSet) YGNodeStyleSetGap(node, YGGutter.Column, s.ColumnGap.Value);
    }

    private static void Size(Node node, Dimension d, Action<Node, float> point, Action<Node, float> percent, Action<Node> auto)
    {
        switch (d.Unit)
        {
            case DimensionUnit.Point: point(node, d.Value); break;
            case DimensionUnit.Percent: percent(node, d.Value); break;
            case DimensionUnit.Auto: auto(node); break;
            case DimensionUnit.Undefined:
            default:
                break;
        }
    }

    // Minimums and maximums have no "auto" in Yoga.
    private static void Limit(Node node, Dimension d, Action<Node, float> point, Action<Node, float> percent)
    {
        switch (d.Unit)
        {
            case DimensionUnit.Point: point(node, d.Value); break;
            case DimensionUnit.Percent: percent(node, d.Value); break;
            case DimensionUnit.Undefined:
            case DimensionUnit.Auto:
            default:
                break;
        }
    }

    private static void Edges(Node node, Edges e, Action<Node, YGEdge, float> point, Action<Node, YGEdge, float> percent, Action<Node, YGEdge>? setAuto)
    {
        // Start and end, not left and right: they swap sides in a right-to-left layout.
        Edge(node, YGEdge.Start, e.Start, point, percent, setAuto);
        Edge(node, YGEdge.Top, e.Top, point, percent, setAuto);
        Edge(node, YGEdge.End, e.End, point, percent, setAuto);
        Edge(node, YGEdge.Bottom, e.Bottom, point, percent, setAuto);
    }

    private static void Edge(Node node, YGEdge edge, Dimension d, Action<Node, YGEdge, float> point, Action<Node, YGEdge, float> percent, Action<Node, YGEdge>? setAuto)
    {
        switch (d.Unit)
        {
            case DimensionUnit.Point: point(node, edge, d.Value); break;
            case DimensionUnit.Percent: percent(node, edge, d.Value); break;
            case DimensionUnit.Auto: setAuto?.Invoke(node, edge); break;
            case DimensionUnit.Undefined:
            default:
                break;
        }
    }

    private static YGFlexDirection Map(FlexDirection d) => d switch
    {
        FlexDirection.Row => YGFlexDirection.Row,
        FlexDirection.RowReverse => YGFlexDirection.RowReverse,
        FlexDirection.ColumnReverse => YGFlexDirection.ColumnReverse,
        _ => YGFlexDirection.Column,
    };

    private static YGJustify Map(Justify j) => j switch
    {
        Justify.Center => YGJustify.Center,
        Justify.FlexEnd => YGJustify.FlexEnd,
        Justify.SpaceBetween => YGJustify.SpaceBetween,
        Justify.SpaceAround => YGJustify.SpaceAround,
        Justify.SpaceEvenly => YGJustify.SpaceEvenly,
        _ => YGJustify.FlexStart,
    };

    private static YGAlign Map(Align a) => a switch
    {
        Align.Auto => YGAlign.Auto,
        Align.FlexStart => YGAlign.FlexStart,
        Align.Center => YGAlign.Center,
        Align.FlexEnd => YGAlign.FlexEnd,
        Align.Baseline => YGAlign.Baseline,
        Align.SpaceBetween => YGAlign.SpaceBetween,
        Align.SpaceAround => YGAlign.SpaceAround,
        Align.SpaceEvenly => YGAlign.SpaceEvenly,
        // Resolved before layout from the parent's direction (UIRoot.ResolveHugs); start until then.
        Align.Hug => YGAlign.FlexStart,
        _ => YGAlign.Stretch,
    };

    private static YGWrap Map(FlexWrap w) => w switch
    {
        FlexWrap.Wrap => YGWrap.Wrap,
        FlexWrap.WrapReverse => YGWrap.WrapReverse,
        _ => YGWrap.NoWrap,
    };

    private static YGPositionType Map(PositionType p) => p switch
    {
        PositionType.Static => YGPositionType.Static,
        PositionType.Absolute => YGPositionType.Absolute,
        _ => YGPositionType.Relative,
    };
}
