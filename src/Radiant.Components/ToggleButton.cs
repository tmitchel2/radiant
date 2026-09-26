using System;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// An icon button that stays on or off (bold, pin, a view mode): filled while on (in the theme's
/// toggle colour, the secondary container by default), its icon filled too. With
/// <see cref="ShowLabel"/> its label shows beside the icon. A button to assistive technology, named
/// by its label and selected while on.
/// Controlled: shows <paramref name="On"/> and reports each press with the new state.
/// </summary>
/// <param name="Icon">The icon.</param>
/// <param name="Label">What it toggles.</param>
/// <param name="On">Whether it's on.</param>
/// <param name="OnChange">Called with the new state.</param>
[RequiresTestId]
public sealed record ToggleButton(string Icon, string Label, bool On, Action<bool>? OnChange) : Component
{
    /// <summary>Tab order: 0 in tree order, negative for one of a roving group's other buttons.</summary>
    public int TabIndex { get; init; }

    /// <summary>Whether it's one of a group only one of which can be on (a radio button to assistive technology).</summary>
    public bool Exclusive { get; init; }

    /// <summary>Whether it can't be used.</summary>
    public bool Disabled { get; init; }

    /// <summary>Whether the label shows beside the icon.</summary>
    public bool ShowLabel { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        System.ArgumentNullException.ThrowIfNull(context);
        var (on, change) = (On, OnChange);
        var theme = context.UseTheme().Theme;
        var labelled = ShowLabel;
        return (On ? SurfaceLooks.Pressable(theme.Components.Button.ToggleOn) : new PressableSurface()) with
        {
            ShowDisabled = Disabled ? true : null,
            ScaleOnPress = true,
            CornerShape = CornerShapeRole.Small,
            Label = Label,
            TabIndex = TabIndex,
            // Heard as on or off: a check box, or a radio button where only one of a group can be on.
            Role = Exclusive ? SemanticsRole.RadioButton : SemanticsRole.CheckBox,
            Checked = On,
            OnPress = () => change?.Invoke(!on),
            Layout = labelled
                ? new LayoutStyle { FlexDirection = FlexDirection.Row, Height = 36, AlignItems = Align.Center, ColumnGap = 6, Padding = Edges.Symmetric(10, 0) }
                : new LayoutStyle { Width = 36, Height = 36, AlignItems = Align.Center, JustifyContent = Justify.Center },
            Children =
            [
                new SurfaceIcon(Icon) { IconSize = labelled ? 18 : 20, IconFilled = On && theme.Components.Icons.FillChosen, Legibility = On ? null : Legibility.Medium },
                labelled ? new SurfaceText(Label) { TextType = TextType.LabelLarge, Legibility = On ? null : Legibility.High } : null,
            ],
        };
    }
}
