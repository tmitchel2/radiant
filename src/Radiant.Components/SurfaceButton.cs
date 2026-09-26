using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A button: a pill-shaped <see cref="PressableSurface"/> with a label. Its variant sets its
/// colours and elevation, and any facet set on the button overrides the variant's.
/// <code>new SurfaceButton("Save", ButtonVariant.Filled) { OnPress = save }</code>
/// </summary>
[ForwardFacets(typeof(PressableSurface), "Container",
    typeof(IHasBackgroundColor), typeof(IHasCornerShape), typeof(IHasElevation), typeof(IHasOutline), typeof(IHasLayout), typeof(IHasPressable))]
[ForwardFacets(typeof(SurfaceText), "Label", typeof(IHasText))]
[ForwardFacets(typeof(SurfaceIcon), "LeadingIcon", typeof(IHasIcon))]
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
        var horizontal = Variant == ButtonVariant.Text ? 12f : 24f;
        var hasIcon = Icon is not null;
        var container = Preset(Variant) with
        {
            CornerShape = CornerShapeRole.Full,
            Layout = new LayoutStyle
            {
                FlexDirection = FlexDirection.Row,
                AlignItems = Align.Center,
                JustifyContent = Justify.Center,
                MinHeight = 40f + theme.DensityOffset,
                // With an icon, the leading side tightens by 8 (Material's 16/24).
                Padding = new Edges(hasIcon ? horizontal - 8f : horizontal, 0, horizontal, 0),
                ColumnGap = 8,
            },
        };
        return ForwardContainer(container) with
        {
            Children =
            [
                hasIcon ? ForwardLeadingIcon(new SurfaceIcon { IconSize = 18f }) : null,
                ForwardLabel(new SurfaceText { TextType = Radiant.Theming.TextType.LabelLarge }),
            ],
        };
    }

    private static PressableSurface Preset(ButtonVariant variant) => variant switch
    {
        ButtonVariant.Tonal => new PressableSurface { SurfaceColor = SurfaceName.Secondary, SurfaceContainerToggle = true },
        ButtonVariant.Outlined => new PressableSurface { ContentColor = SurfaceName.Primary, ShowOutline = true },
        ButtonVariant.Text => new PressableSurface { ContentColor = SurfaceName.Primary },
        ButtonVariant.Elevated => new PressableSurface
        {
            SurfaceColor = SurfaceName.SurfaceContainerLow,
            ContentColor = SurfaceName.Primary,
            Elevation = ElevationLevel.Level1,
        },
        _ => new PressableSurface { SurfaceColor = SurfaceName.Primary },
    };
}
