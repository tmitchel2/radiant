using Radiant.Layout;
using Radiant.Text;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// An icon from Material Symbols in the content colour of the surface it's on. Decorative: give
/// the control it's in a label for assistive technology.
/// </summary>
public sealed partial record SurfaceIcon : Component, IHasIcon, IHasLayout
{
    /// <summary>The icon named <paramref name="icon"/>.</summary>
    public SurfaceIcon(string? icon = null) => Icon = icon;

    /// <summary>A legibility to draw at instead of the surface's content legibility.</summary>
    public float? Legibility { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        System.ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var surface = context.UseSurface();
        if (Legibility is { } legibility)
        {
            surface = surface with { Content = surface.Content with { Opacity = legibility } };
        }
        var size = IconSize ?? 24f;
        return new TextBlock(Icon ?? "")
        {
            IsDecorative = true,
            Wrap = false,
            Style = new TextStyle
            {
                FontFamily = FontLibrary.Icons,
                Size = size,
                LineHeight = size,
                Color = theme.ContentColor(surface),
                Variations = IconFilled == true ? s_filled : [],
            },
            Layout = Layout ?? new LayoutStyle { Width = size, Height = size },
        };
    }

    private static readonly FontVariation[] s_filled = [new(FontVariation.Fill, 1f)];
}
