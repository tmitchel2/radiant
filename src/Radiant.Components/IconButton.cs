using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A button that is just an icon, in a 40 px circle. It needs a <see cref="Label"/>: with no text,
/// that is what assistive technology reads.
/// <code>new IconButton("delete", "Delete") { OnPress = remove }</code>
/// </summary>
[ForwardFacets(typeof(PressableSurface), "Container",
    typeof(IHasBackgroundColor), typeof(IHasCornerShape), typeof(IHasOutline), typeof(IHasLayout), typeof(IHasPressable))]
[ForwardFacets(typeof(SurfaceIcon), "Glyph", typeof(IHasIcon))]
[RequiresTestId]
public sealed partial record IconButton : Component, IHasBackgroundColor, IHasCornerShape, IHasOutline, IHasLayout, IHasPressable, IHasIcon
{
    /// <summary>An icon button.</summary>
    /// <param name="icon">The Material Symbols name.</param>
    /// <param name="label">What it does, for assistive technology.</param>
    /// <param name="variant">How prominent it is.</param>
    public IconButton(string icon, string label, IconButtonVariant variant = IconButtonVariant.Standard)
    {
        Icon = icon;
        Label = label;
        Variant = variant;
    }

    /// <summary>What the button does, read by assistive technology.</summary>
    public string Label { get; init; }

    /// <summary>How prominent it is.</summary>
    public IconButtonVariant Variant { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        System.ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var style = theme.Theme.Components.IconButton;
        var size = style.Size + theme.DensityOffset;
        var look = Variant switch
        {
            IconButtonVariant.Filled => style.Filled,
            IconButtonVariant.Tonal => style.Tonal,
            IconButtonVariant.Outlined => style.Outlined,
            _ => style.Standard,
        };
        return ForwardContainer(SurfaceLooks.Pressable(look) with
        {
            CornerShape = style.Shape,
            Label = Label,
            Layout = new LayoutStyle { Width = size, Height = size, AlignItems = Align.Center, JustifyContent = Justify.Center },
        }) with
        {
            Children = [ForwardGlyph(new SurfaceIcon { IconSize = style.IconSize })],
        };
    }
}
