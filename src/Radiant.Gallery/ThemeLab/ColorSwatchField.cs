using System;
using Radiant.Components;
using Radiant.Components.Primitives;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;
using Color = Radiant.Graphics2D.Color;

namespace Radiant.Gallery.ThemeLab;

/// <summary>
/// A colour in a property grid: its hex and a swatch, opening a colour picker in a popover. Small
/// enough for a palette's worth of rows.
/// </summary>
/// <param name="Name">The colour's name, for the picker and assistive technology.</param>
/// <param name="Value">The colour.</param>
/// <param name="OnChange">Called as the colour is picked.</param>
internal sealed partial record ColorSwatchField(string Name, Color Value, Action<Color> OnChange) : Component
{
    [TestId<SurfaceButton>] public static partial string Swatch { get; }
    [TestId<ColorPicker>] public static partial string Picker { get; }

    public override Element? Build(BuildContext context)
    {
        var theme = context.UseTheme();
        var anchor = context.UseRef(new ElementRef()).Value;
        var open = context.UseState(false);
        var change = OnChange;
        var argb = unchecked((int)Value.ToArgb());
        return new Fragment(
            new Box
            {
                Ref = anchor,
                Children =
                [
                    new SurfaceButton(ColorPicker.Hex(argb), ButtonVariant.Outlined)
                    {
                        TestId = Swatch,
                        OnPress = () => open.Set(true),
                        // Edged, so a colour close to the button's own still shows.
                        Trailing = new Box
                        {
                            Layout = new LayoutStyle { Width = 18, Height = 18 },
                            Background = Value,
                            BorderWidth = 1,
                            BorderColor = theme.OutlineVariant,
                            CornerRadii = Radiant.Graphics2D.CornerRadii.All(4),
                        },
                    },
                ],
            },
            new Popover(anchor, open.Value, () => open.Set(false), new ColorPicker(argb, picked => change(Color.FromArgb(picked))) { TestId = Picker, Label = Name })
            {
                Label = Name,
                Align = SideAlign.Start,
            });
    }
}
