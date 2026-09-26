using System;
using System.Collections.Generic;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>A closing pitch: a tinted panel with a headline, a sentence, and the actions, centred.</summary>
/// <param name="Headline">The headline.</param>
public sealed record CallToAction(string Headline) : Component
{
    /// <summary>A sentence under the headline.</summary>
    public string? Text { get; init; }

    /// <summary>The actions (a filled button and a text one, usually).</summary>
    public IReadOnlyList<Element?> Actions { get; init; } = [];

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new Surface
        {
            SurfaceColor = SurfaceName.Primary,
            SurfaceContainerToggle = true,
            CornerShape = CornerShapeRole.ExtraLarge,
            Layout = new LayoutStyle { AlignSelf = Align.Stretch, AlignItems = Align.Center, RowGap = 16, Padding = Edges.Symmetric(24, 48) },
            Children =
            [
                new SurfaceText(Headline) { TextType = TextType.HeadlineMedium, HeadingLevel = 2, Alignment = Radiant.Text.TextAlignment.Center },
                Text is null ? null : new SurfaceText(Text) { TextType = TextType.BodyLarge, Alignment = Radiant.Text.TextAlignment.Center, Layout = new LayoutStyle { MaxWidth = 560 } },
                Actions.Count == 0 ? null : new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, FlexWrap = FlexWrap.Wrap, JustifyContent = Justify.Center, ColumnGap = 12, RowGap = 8, Margin = new Edges(0, 8, 0, 0) },
                    Children = Actions,
                },
            ],
        };
    }
}
