using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// Text in the content colour of the surface it's on, in a step of the theme's type scale:
/// readable on whatever surface holds it.
/// </summary>
public sealed partial record SurfaceText : Component, IHasText, IHasLayout
{
    /// <summary>Text in the surface's content colour.</summary>
    public SurfaceText(string? text = null) => Text = text;

    /// <summary>The most lines to show; null for all.</summary>
    public int? MaxLines { get; init; }

    /// <summary>Where lines sit.</summary>
    public Radiant.Text.TextAlignment Alignment { get; init; }

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
        return new TextBlock(Text ?? "")
        {
            Style = theme.Text(TextType ?? Radiant.Theming.TextType.BodyMedium) with { Color = theme.ContentColor(surface) },
            MaxLines = MaxLines,
            Alignment = Alignment,
            Layout = Layout ?? default,
        };
    }
}
