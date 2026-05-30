using System;
using System.Collections.Generic;
using System.Numerics;
using Facebook.Yoga;
using Radiant.UI;
using static Facebook.Yoga.YGNodeAPI;
using static Facebook.Yoga.YGNodeStyleAPI;
using static Facebook.Yoga.YGNodeLayoutAPI;

namespace Radiant.Layout;

/// <summary>
/// Drives flexbox layout for the Radiant widget tree using the vendored Yoga engine.
///
/// <para>For each opted-in layout root (<see cref="UIElement.ParticipatesInLayout"/>) it builds a
/// <em>transient</em> Yoga node tree from the elements' <see cref="LayoutStyle"/>, runs Yoga, writes
/// the computed rectangles back into each child's <see cref="UIElement.Position"/>/<see cref="UIElement.Size"/>,
/// then frees the Yoga tree. Yoga nodes never outlive a layout pass, so they don't couple to widget
/// lifetime.</para>
///
/// <para>Participation is decided at the root: once a root opts in, its <em>entire</em> subtree is
/// laid out from <see cref="LayoutStyle"/> (unset properties keep Yoga's defaults). The root's own
/// <see cref="UIElement.Position"/>/<see cref="UIElement.Size"/> are owned by the caller (e.g. window
/// anchoring) and used as the available space; only descendants are written back.</para>
/// </summary>
public static class YogaLayoutEngine
{
    /// <summary>Lays out every opted-in, visible root in <paramref name="roots"/> against the viewport.</summary>
    public static void Calculate(IReadOnlyList<UIElement> roots, Vector2 viewport)
    {
        foreach (var root in roots)
        {
            if (root.Visible && root.ParticipatesInLayout)
            {
                CalculateRoot(root, viewport);
            }
        }
    }

    /// <summary>
    /// Lays out a single root and its subtree. The root's current <see cref="UIElement.Size"/> is the
    /// available space (falling back to <paramref name="viewport"/> on a zero axis); the root's
    /// <see cref="UIElement.Position"/> is the origin for its children.
    /// </summary>
    public static void CalculateRoot(UIElement root, Vector2 viewport)
    {
        var map = new Dictionary<UIElement, Node>();
        var rootNode = Build(root, map);

        var availableWidth = root.Size.X > 0f ? root.Size.X : viewport.X;
        var availableHeight = root.Size.Y > 0f ? root.Size.Y : viewport.Y;
        YGNodeCalculateLayout(rootNode, availableWidth, availableHeight, YGDirection.LTR);

        WriteBackChildren(root, root.Position, map);
        YGNodeFreeRecursive(rootNode);
    }

    private static Node Build(UIElement element, Dictionary<UIElement, Node> map)
    {
        var node = YGNodeNew();
        map[element] = node;
        ApplyStyle(node, element.Layout);

        // ILayoutBoundary containers (e.g. ScrollView) are sized as a leaf — their children keep
        // their own positioning scheme, so the walk stops here.
        if (element is IUiContainer container && element is not ILayoutBoundary && container.Children.Count > 0)
        {
            var index = 0;
            foreach (var child in container.Children)
            {
                YGNodeInsertChild(node, Build(child, map), (nuint)index);
                index++;
            }
        }
        else if (element is ILayoutMeasurable measurable)
        {
            // Yoga measures content-sized leaves through this callback; the widget keeps its font private.
            YGNodeSetMeasureFunc(node, (_, availableWidth, _, _, _) =>
            {
                var size = measurable.MeasureContent(availableWidth);
                return new YGSize { Width = size.X, Height = size.Y };
            });
        }

        return node;
    }

    private static void WriteBackChildren(UIElement element, Vector2 origin, Dictionary<UIElement, Node> map)
    {
        if (element is not IUiContainer container || element is ILayoutBoundary) return;

        foreach (var child in container.Children)
        {
            var node = map[child];
            var childPosition = origin + new Vector2(YGNodeLayoutGetLeft(node), YGNodeLayoutGetTop(node));
            child.Position = childPosition;
            child.Size = new Vector2(YGNodeLayoutGetWidth(node), YGNodeLayoutGetHeight(node));
            WriteBackChildren(child, childPosition, map);
        }
    }

    private static void ApplyStyle(Node node, LayoutStyle s)
    {
        if (s.FlexDirection is { } flexDirection) YGNodeStyleSetFlexDirection(node, MapFlexDirection(flexDirection));
        if (s.JustifyContent is { } justify) YGNodeStyleSetJustifyContent(node, MapJustify(justify));
        if (s.AlignItems is { } alignItems) YGNodeStyleSetAlignItems(node, MapAlign(alignItems));
        if (s.AlignSelf is { } alignSelf) YGNodeStyleSetAlignSelf(node, MapAlign(alignSelf));
        if (s.FlexWrap is { } wrap) YGNodeStyleSetFlexWrap(node, MapWrap(wrap));
        if (s.Position is { } position) YGNodeStyleSetPositionType(node, MapPositionType(position));
        if (s.FlexGrow is { } grow) YGNodeStyleSetFlexGrow(node, grow);
        if (s.FlexShrink is { } shrink) YGNodeStyleSetFlexShrink(node, shrink);
        if (s.AspectRatio is { } aspectRatio) YGNodeStyleSetAspectRatio(node, aspectRatio);

        ApplyFlexBasis(node, s.FlexBasis);
        ApplyDimension(node, s.Width, YGNodeStyleSetWidth, YGNodeStyleSetWidthPercent, YGNodeStyleSetWidthAuto);
        ApplyDimension(node, s.Height, YGNodeStyleSetHeight, YGNodeStyleSetHeightPercent, YGNodeStyleSetHeightAuto);
        ApplyMinMax(node, s.MinWidth, YGNodeStyleSetMinWidth, YGNodeStyleSetMinWidthPercent);
        ApplyMinMax(node, s.MinHeight, YGNodeStyleSetMinHeight, YGNodeStyleSetMinHeightPercent);
        ApplyMinMax(node, s.MaxWidth, YGNodeStyleSetMaxWidth, YGNodeStyleSetMaxWidthPercent);
        ApplyMinMax(node, s.MaxHeight, YGNodeStyleSetMaxHeight, YGNodeStyleSetMaxHeightPercent);
        ApplyEdges(node, s.Margin, YGNodeStyleSetMargin, YGNodeStyleSetMarginPercent, YGNodeStyleSetMarginAuto);
        ApplyEdges(node, s.Padding, YGNodeStyleSetPadding, YGNodeStyleSetPaddingPercent, setAuto: null);
        ApplyEdges(node, s.Inset, YGNodeStyleSetPosition, YGNodeStyleSetPositionPercent, setAuto: null);

        if (s.RowGap.IsSet) YGNodeStyleSetGap(node, YGGutter.Row, s.RowGap.Value);
        if (s.ColumnGap.IsSet) YGNodeStyleSetGap(node, YGGutter.Column, s.ColumnGap.Value);
    }

    private static void ApplyDimension(
        Node node, Dimension d,
        Action<Node, float> setPoint, Action<Node, float> setPercent, Action<Node> setAuto)
    {
        switch (d.Unit)
        {
            case DimensionUnit.Point: setPoint(node, d.Value); break;
            case DimensionUnit.Percent: setPercent(node, d.Value); break;
            case DimensionUnit.Auto: setAuto(node); break;
            case DimensionUnit.Undefined: break;
            default: break;
        }
    }

    private static void ApplyMinMax(
        Node node, Dimension d,
        Action<Node, float> setPoint, Action<Node, float> setPercent)
    {
        // Min/max constraints have no "auto" in Yoga.
        switch (d.Unit)
        {
            case DimensionUnit.Point: setPoint(node, d.Value); break;
            case DimensionUnit.Percent: setPercent(node, d.Value); break;
            case DimensionUnit.Undefined:
            case DimensionUnit.Auto:
            default:
                break;
        }
    }

    private static void ApplyFlexBasis(Node node, Dimension d)
    {
        switch (d.Unit)
        {
            case DimensionUnit.Point: YGNodeStyleSetFlexBasis(node, d.Value); break;
            case DimensionUnit.Percent: YGNodeStyleSetFlexBasisPercent(node, d.Value); break;
            case DimensionUnit.Auto: YGNodeStyleSetFlexBasisAuto(node); break;
            case DimensionUnit.Undefined:
            default:
                break;
        }
    }

    private static void ApplyEdges(
        Node node, Edges e,
        Action<Node, YGEdge, float> setPoint, Action<Node, YGEdge, float> setPercent, Action<Node, YGEdge>? setAuto)
    {
        ApplyEdge(node, YGEdge.Left, e.Left, setPoint, setPercent, setAuto);
        ApplyEdge(node, YGEdge.Top, e.Top, setPoint, setPercent, setAuto);
        ApplyEdge(node, YGEdge.Right, e.Right, setPoint, setPercent, setAuto);
        ApplyEdge(node, YGEdge.Bottom, e.Bottom, setPoint, setPercent, setAuto);
    }

    private static void ApplyEdge(
        Node node, YGEdge edge, Dimension d,
        Action<Node, YGEdge, float> setPoint, Action<Node, YGEdge, float> setPercent, Action<Node, YGEdge>? setAuto)
    {
        switch (d.Unit)
        {
            case DimensionUnit.Point: setPoint(node, edge, d.Value); break;
            case DimensionUnit.Percent: setPercent(node, edge, d.Value); break;
            case DimensionUnit.Auto: setAuto?.Invoke(node, edge); break;
            case DimensionUnit.Undefined:
            default:
                break;
        }
    }

    private static YGFlexDirection MapFlexDirection(FlexDirection d) => d switch
    {
        FlexDirection.Column => YGFlexDirection.Column,
        FlexDirection.ColumnReverse => YGFlexDirection.ColumnReverse,
        FlexDirection.Row => YGFlexDirection.Row,
        FlexDirection.RowReverse => YGFlexDirection.RowReverse,
        _ => YGFlexDirection.Column,
    };

    private static YGJustify MapJustify(Justify j) => j switch
    {
        Justify.FlexStart => YGJustify.FlexStart,
        Justify.Center => YGJustify.Center,
        Justify.FlexEnd => YGJustify.FlexEnd,
        Justify.SpaceBetween => YGJustify.SpaceBetween,
        Justify.SpaceAround => YGJustify.SpaceAround,
        Justify.SpaceEvenly => YGJustify.SpaceEvenly,
        _ => YGJustify.FlexStart,
    };

    private static YGAlign MapAlign(Align a) => a switch
    {
        Align.Auto => YGAlign.Auto,
        Align.FlexStart => YGAlign.FlexStart,
        Align.Center => YGAlign.Center,
        Align.FlexEnd => YGAlign.FlexEnd,
        Align.Stretch => YGAlign.Stretch,
        Align.Baseline => YGAlign.Baseline,
        Align.SpaceBetween => YGAlign.SpaceBetween,
        Align.SpaceAround => YGAlign.SpaceAround,
        Align.SpaceEvenly => YGAlign.SpaceEvenly,
        _ => YGAlign.Stretch,
    };

    private static YGWrap MapWrap(FlexWrap w) => w switch
    {
        FlexWrap.NoWrap => YGWrap.NoWrap,
        FlexWrap.Wrap => YGWrap.Wrap,
        FlexWrap.WrapReverse => YGWrap.WrapReverse,
        _ => YGWrap.NoWrap,
    };

    private static YGPositionType MapPositionType(PositionType p) => p switch
    {
        PositionType.Static => YGPositionType.Static,
        PositionType.Relative => YGPositionType.Relative,
        PositionType.Absolute => YGPositionType.Absolute,
        _ => YGPositionType.Relative,
    };
}
