using Radiant.Graphics2D;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>What <see cref="Slider"/> and <see cref="RangeSlider"/> draw alike: a thumb as the theme styles it.</summary>
internal static class SliderParts
{
    /// <summary>
    /// A 20 px thumb, filled with <paramref name="active"/> and raised, or white ringed in it; without
    /// halos, keyboard focus rings it.
    /// </summary>
    public static Element?[] Thumb(ResolvedTheme theme, Color active, bool focused, bool raised)
    {
        var style = theme.Theme.Components.Selection;
        var interaction = theme.Theme.Components.Interaction;
        var ringed = style.SliderThumb == SliderThumb.Ring;
        var ringOut = interaction.FocusRingWidth + interaction.FocusRingGap;
        return
        [
            new Box
            {
                HitTestVisible = false,
                Layout = new LayoutStyle { Width = 20, Height = 20 },
                Background = ringed ? theme.Get(SurfaceName.SurfaceBright) : active,
                BorderWidth = ringed ? 2f : 0f,
                BorderColor = active,
                CornerRadii = CornerRadii.All(10),
                Shadows = raised ? theme.Elevation(ElevationLevel.Level1) : [],
            },
            style.Halo || !focused ? null : new Box
            {
                HitTestVisible = false,
                Layout = new LayoutStyle { Position = PositionType.Absolute, Width = 20 + 2 * ringOut, Height = 20 + 2 * ringOut },
                BorderWidth = interaction.FocusRingWidth,
                BorderColor = theme.Get(interaction.FocusRingColor),
                CornerRadii = CornerRadii.All(10 + ringOut),
            },
        ];
    }
}
