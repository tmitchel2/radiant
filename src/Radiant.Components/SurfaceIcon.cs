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

    /// <summary>
    /// Whether an icon that points along the line (back, forward, chevrons, first and last page)
    /// is drawn as its mirror when the UI reads right to left, so "back" still points to where
    /// things came from. On by default.
    /// </summary>
    public bool MirrorInRightToLeft { get; init; } = true;

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
        var rightToLeft = context.UseRightToLeft();
        var size = IconSize ?? 24f;
        var icon = Icon ?? "";
        if (rightToLeft && MirrorInRightToLeft && s_mirrors.TryGetValue(icon, out var mirror))
        {
            icon = mirror;
        }
        return new TextBlock(icon)
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
            Layout = new LayoutStyle { Width = size, Height = size }.Merge(Layout ?? default),
        };
    }

    private static readonly FontVariation[] s_filled = [new(FontVariation.Fill, 1f)];

    // Icons that point along the line, each with the one that points the other way.
    private static readonly System.Collections.Frozen.FrozenDictionary<string, string> s_mirrors = Pairs(
        ("arrow_back", "arrow_forward"),
        ("arrow_back_ios", "arrow_forward_ios"),
        ("arrow_left", "arrow_right"),
        ("chevron_left", "chevron_right"),
        ("navigate_before", "navigate_next"),
        ("first_page", "last_page"),
        ("keyboard_arrow_left", "keyboard_arrow_right"),
        ("keyboard_double_arrow_left", "keyboard_double_arrow_right"),
        ("keyboard_tab", "keyboard_tab_rtl"),
        ("west", "east"));

    private static System.Collections.Frozen.FrozenDictionary<string, string> Pairs(params (string A, string B)[] pairs)
    {
        var map = new System.Collections.Generic.Dictionary<string, string>();
        foreach (var (a, b) in pairs)
        {
            map.TryAdd(a, b);
            map.TryAdd(b, a);
        }
        return System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(map);
    }
}
