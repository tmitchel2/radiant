using System;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>An item in a <see cref="StatusBar"/>: a short text with an optional icon, pressable if it does something.</summary>
/// <param name="Text">What it says ("Ln 12, Col 4", "main").</param>
public sealed record StatusItem(string Text) : Component
{
    /// <summary>An icon before the text.</summary>
    public string? Icon { get; init; }

    /// <summary>What pressing it does; null for an item that only shows something.</summary>
    public Action? OnPress { get; init; }

    /// <summary>What assistive technology calls it, when the text alone isn't enough.</summary>
    public string? Label { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        Element?[] children =
        [
            Icon is null ? null : new SurfaceIcon(Icon) { IconSize = 14 },
            new SurfaceText(Text) { TextType = TextType.LabelMedium, MaxLines = 1 },
        ];
        var layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 4, Height = 22, Padding = Edges.Symmetric(6, 0) };
        return OnPress is null
            ? new Box { Layout = layout, Semantics = Label is null ? null : new Semantics { Role = SemanticsRole.Text, Label = Label }, Children = children }
            : new PressableSurface { InsetFocusRing = true, OnPress = OnPress, Label = Label, CornerShape = CornerShapeRole.ExtraSmall, Layout = layout, Children = children };
    }
}
