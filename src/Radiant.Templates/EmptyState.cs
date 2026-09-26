using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>What a list or page shows before there's anything in it: an icon, a title, a line of help and an action.</summary>
/// <param name="Icon">The icon.</param>
/// <param name="Title">The title.</param>
public sealed record EmptyState(string Icon, string Title) : Component
{
    /// <summary>A line of help.</summary>
    public string? Description { get; init; }

    /// <summary>The way forward, such as a button.</summary>
    public Element? Action { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        var style = context.UseTheme().Theme.Components.Showcase;
        return new Box
        {
            Layout = new LayoutStyle { AlignItems = Align.Center, Padding = Edges.All(40), RowGap = 12 },
            Children =
            [
                SurfaceLooks.Surface(style.EmptyIcon) with
                {
                    CornerShape = style.EmptyIconShape,
                    Layout = new LayoutStyle { Width = 72, Height = 72, AlignItems = Align.Center, JustifyContent = Justify.Center },
                    Children = [new SurfaceIcon(Icon) { IconSize = 36 }],
                },
                new SurfaceText(Title) { TextType = TextType.TitleLarge, Alignment = Radiant.Text.TextAlignment.Center },
                Description is null ? null : new SurfaceText(Description) { Legibility = Legibility.Medium, Alignment = Radiant.Text.TextAlignment.Center, Layout = new LayoutStyle { MaxWidth = 360 } },
                Action,
            ],
        };
    }
}
