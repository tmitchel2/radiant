using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A button: a <see cref="PressableSurface"/> with a label, shaped as the theme shapes controls (a pill by default). Its variant sets its
/// colours and elevation, and any facet set on the button overrides the variant's.
/// <code>new SurfaceButton("Save", ButtonVariant.Filled) { OnPress = save }</code>
/// </summary>
[ForwardFacets(typeof(PressableSurface), "Container",
    typeof(IHasBackgroundColor), typeof(IHasCornerShape), typeof(IHasElevation), typeof(IHasOutline), typeof(IHasLayout), typeof(IHasPressable))]
[ForwardFacets(typeof(SurfaceText), "Label", typeof(IHasText))]
[ForwardFacets(typeof(SurfaceIcon), "LeadingIcon", typeof(IHasIcon))]
[RequiresTestId]
public sealed partial record SurfaceButton : Component, IHasBackgroundColor, IHasCornerShape, IHasElevation, IHasOutline, IHasLayout, IHasPressable, IHasText, IHasIcon
{
    /// <summary>A button with a label.</summary>
    public SurfaceButton(string text, ButtonVariant variant = ButtonVariant.Filled)
    {
        Text = text;
        Variant = variant;
    }

    /// <summary>How prominent the button is.</summary>
    public ButtonVariant Variant { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        System.ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var style = theme.Theme.Components.Button;
        var horizontal = Variant == ButtonVariant.Text ? style.TextPadding : style.Padding;
        var hasIcon = Icon is not null;
        var container = SurfaceLooks.Pressable(Look(style, Variant)) with
        {
            CornerShape = style.Shape,
            Layout = new LayoutStyle
            {
                FlexDirection = FlexDirection.Row,
                AlignItems = Align.Center,
                JustifyContent = Justify.Center,
                MinHeight = style.Height + theme.DensityOffset,
                // With an icon, the leading side tightens by a third of the padding (Material's 16/24).
                Padding = new Edges(hasIcon ? System.MathF.Max(4f, horizontal - style.Padding / 3f) : horizontal, 0, horizontal, 0),
                ColumnGap = 8,
            },
        };
        return ForwardContainer(container) with
        {
            Children =
            [
                hasIcon ? ForwardLeadingIcon(new SurfaceIcon { IconSize = style.IconSize }) : null,
                ForwardLabel(new SurfaceText { TextType = style.Label }),
            ],
        };
    }

    private static SurfaceLook Look(ButtonStyle style, ButtonVariant variant) => variant switch
    {
        ButtonVariant.Tonal => style.Tonal,
        ButtonVariant.Outlined => style.Outlined,
        ButtonVariant.Text => style.Text,
        ButtonVariant.Elevated => style.Elevated,
        _ => style.Filled,
    };
}
