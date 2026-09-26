using System;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A row of a list: an optional leading icon, a headline, optional supporting text, and optional
/// trailing text or icon. One line is 56 px tall, two 72, three 88. Pressable if given
/// <see cref="OnPress"/>.
/// </summary>
/// <param name="Headline">The main text.</param>
public sealed record ListItem(string Headline) : Component
{
    /// <summary>Secondary text under the headline.</summary>
    public string? SupportingText { get; init; }

    /// <summary>How many lines the supporting text may take (1 or 2).</summary>
    public int SupportingLines { get; init; } = 1;

    /// <summary>A leading icon.</summary>
    public string? LeadingIcon { get; init; }

    /// <summary>A trailing icon.</summary>
    public string? TrailingIcon { get; init; }

    /// <summary>Trailing text, such as a count or a shortcut.</summary>
    public string? TrailingText { get; init; }

    /// <summary>What pressing the row does; null for a static row.</summary>
    public Action? OnPress { get; init; }

    /// <summary>Whether it's selected (a navigation list's current item).</summary>
    public bool Selected { get; init; }

    /// <summary>Whether it can't be used.</summary>
    public bool Disabled { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var lines = SupportingText is null ? 1 : 1 + Math.Clamp(SupportingLines, 1, 2);
        var height = (lines switch { 1 => 56f, 2 => 72f, _ => 88f }) + theme.DensityOffset;
        Element?[] children =
        [
            LeadingIcon is null ? null : new SurfaceIcon(LeadingIcon) { Legibility = Legibility.Medium, IconFilled = Selected },
            new Box
            {
                Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 },
                Children =
                [
                    new SurfaceText(Headline) { TextType = TextType.BodyLarge, MaxLines = 1 },
                    SupportingText is null ? null : new SurfaceText(SupportingText)
                    {
                        TextType = TextType.BodyMedium,
                        Legibility = Legibility.Medium,
                        MaxLines = SupportingLines,
                    },
                ],
            },
            TrailingText is null ? null : new SurfaceText(TrailingText) { TextType = TextType.LabelSmall, Legibility = Legibility.Medium },
            TrailingIcon is null ? null : new SurfaceIcon(TrailingIcon) { Legibility = Legibility.Medium },
        ];
        var layout = new LayoutStyle
        {
            FlexDirection = FlexDirection.Row,
            AlignItems = Align.Center,
            MinHeight = height,
            Padding = new Edges(16, 8, 24, 8),
            ColumnGap = 16,
        };
        if (OnPress is null && !Selected)
        {
            return new Box { Layout = layout, Children = children };
        }
        return new PressableSurface
        {
            SurfaceColor = Selected ? SurfaceName.Secondary : null,
            SurfaceContainerToggle = Selected ? true : null,
            ShowDisabled = Disabled ? true : null,
            OnPress = OnPress,
            Role = SemanticsRole.ListItem,
            Selected = Selected,
            Layout = layout,
            Children = children,
        };
    }
}
