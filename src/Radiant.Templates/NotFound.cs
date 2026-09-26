using System;
using System.Collections.Generic;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>A "page not found" screen: the code, a title, a sentence and ways back, centred.</summary>
public sealed record NotFound : Component
{
    /// <summary>The large code over the title.</summary>
    public string Code { get; init; } = "404";

    /// <summary>The title.</summary>
    public string Title { get; init; } = "Page not found";

    /// <summary>The sentence under it.</summary>
    public string Text { get; init; } = "Sorry, we couldn't find the page you're looking for.";

    /// <summary>The ways back (a Home button, a Contact link).</summary>
    public IReadOnlyList<Element?> Actions { get; init; } = [];

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var accent = context.UseSurface().With(new SurfaceChange { Content = SurfaceName.Primary });
        return new Box
        {
            Layout = new LayoutStyle { AlignSelf = Align.Stretch, AlignItems = Align.Center, RowGap = 12, Padding = Edges.Symmetric(24, 48) },
            Children =
            [
                ThemeContexts.Surface.Provide(accent, new SurfaceText(Code) { TextType = TextType.DisplayMedium }),
                new SurfaceText(Title) { TextType = TextType.HeadlineMedium, HeadingLevel = 1, Alignment = Radiant.Text.TextAlignment.Center },
                new SurfaceText(Text) { Legibility = Legibility.Medium, Alignment = Radiant.Text.TextAlignment.Center, Layout = new LayoutStyle { MaxWidth = 420 } },
                Actions.Count == 0 ? null : new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, ColumnGap = 12, Margin = new Edges(0, 12, 0, 0) },
                    Children = Actions,
                },
            ],
        };
    }
}
