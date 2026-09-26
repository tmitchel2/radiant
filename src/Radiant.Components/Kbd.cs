using Radiant.Layout;
using Radiant.Text;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>A keyboard key, or a chord of them, drawn as keycaps: "⌘ K" in a help line or a menu.</summary>
/// <param name="Keys">What the keys say, one per cap ("⌘", "K"); see <see cref="For(KeyChord)"/>.</param>
public sealed record Kbd(params string[] Keys) : Component
{
    /// <summary>The caps for a chord, as the platform writes it.</summary>
    public static Kbd For(KeyChord chord)
    {
        var text = chord.ToString();
        // Elsewhere chords are joined by "+" ("Ctrl+Shift+P"); macOS runs symbols together with
        // the key's name at the end ("⇧⌘P", "⌘Esc"), one cap per symbol.
        if (text.Contains('+', System.StringComparison.Ordinal) && text.Length > 1)
        {
            return new Kbd(text.Split('+'));
        }
        var caps = new System.Collections.Generic.List<string>();
        var at = 0;
        while (at < text.Length && "⌃⌥⇧⌘".Contains(text[at], System.StringComparison.Ordinal))
        {
            caps.Add(text[at].ToString());
            at++;
        }
        caps.Add(text[at..]);
        return new Kbd([.. caps]);
    }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        System.ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var surface = context.UseSurface();
        var style = theme.Text(TextType.LabelMedium) with { FontFamily = FontLibrary.JetBrainsMono, Color = theme.ContentColor(surface with { Content = surface.Content with { Opacity = Legibility.Medium } }) };
        var caps = new Element?[Keys.Length];
        for (var i = 0; i < Keys.Length; i++)
        {
            caps[i] = new Box
            {
                Layout = new LayoutStyle { MinWidth = 20, Height = 20, Padding = Edges.Symmetric(5, 0), AlignItems = Align.Center, JustifyContent = Justify.Center },
                BorderWidth = 1f,
                BorderColor = theme.OutlineVariant,
                CornerRadii = theme.Corners(CornerShapeRole.ExtraSmall),
                Children = [new TextBlock(Keys[i]) { Style = style, IsDecorative = true }],
            };
        }
        return new Box
        {
            Semantics = new Semantics { Role = SemanticsRole.Text, Label = string.Join(" ", Keys) },
            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, ColumnGap = 3 },
            Children = caps,
        };
    }
}
