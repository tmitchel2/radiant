using System.Collections.Generic;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>
/// The opening of a landing page: an eyebrow, a large headline, a paragraph, and call-to-action
/// buttons, beside an optional picture, on a tinted container.
/// </summary>
/// <param name="Headline">The headline.</param>
public sealed record Hero(string Headline) : Component
{
    /// <summary>A short line above the headline.</summary>
    public string? Eyebrow { get; init; }

    /// <summary>The paragraph under it.</summary>
    public string? Text { get; init; }

    /// <summary>The buttons.</summary>
    public IReadOnlyList<Element?> Actions { get; init; } = [];

    /// <summary>A picture beside the text.</summary>
    public ImageSource? Picture { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context) => new Surface
    {
        SurfaceColor = SurfaceName.Primary,
        SurfaceContainerToggle = true,
        CornerShape = CornerShapeRole.ExtraLarge,
        ClipContent = true,
        Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, FlexWrap = FlexWrap.Wrap, AlignItems = Align.Center, Padding = Edges.All(40), ColumnGap = 40, RowGap = 24 },
        Children =
        [
            new Box
            {
                Layout = new LayoutStyle { FlexBasis = 320, FlexGrow = 1, RowGap = 16 },
                Children =
                [
                    Eyebrow is null ? null : new SurfaceText(Eyebrow) { TextType = TextType.LabelLarge },
                    new SurfaceText(Headline) { TextType = TextType.DisplaySmall, HeadingLevel = 1 },
                    Text is null ? null : new SurfaceText(Text) { TextType = TextType.BodyLarge, Legibility = Legibility.Medium },
                    new Box { Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, ColumnGap = 12, Margin = new Edges(0, 8, 0, 0) }, Children = Actions },
                ],
            },
            Picture is null ? null : new Image(Picture)
            {
                Fit = ImageFit.Cover,
                CornerRadii = Radiant.Graphics2D.CornerRadii.All(24),
                Layout = new LayoutStyle { FlexBasis = 280, FlexGrow = 1, Height = 260 },
            },
        ],
    };
}
