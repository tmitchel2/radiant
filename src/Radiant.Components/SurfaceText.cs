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

    /// <summary>The text's level as a heading (1 is the top), or 0 when it isn't one.</summary>
    public int HeadingLevel { get; init; }

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
        // Overlines are set in capitals.
        var text = TextType == Radiant.Theming.TextType.Overline ? (Text ?? "").ToUpper(System.Globalization.CultureInfo.CurrentCulture) : Text ?? "";
        return new TextBlock(text)
        {
            Style = theme.Text(TextType ?? Radiant.Theming.TextType.BodyMedium) with { Color = theme.ContentColor(surface) },
            MaxLines = MaxLines,
            Alignment = Alignment,
            HeadingLevel = HeadingLevel,
            Layout = Layout ?? default,
        };
    }
}
