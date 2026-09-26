using System.Collections.Generic;
using System.Globalization;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// People as overlapping avatars (a document's collaborators), each ringed in the surface colour so
/// they read apart, the rest counted as "+N". Assistive technology hears every name.
/// </summary>
/// <param name="Names">The people.</param>
public sealed record AvatarGroup(IReadOnlyList<string> Names) : Component
{
    /// <summary>The most avatars shown before the rest are counted.</summary>
    public int Max { get; init; } = 4;

    /// <summary>Each avatar's size.</summary>
    public float Size { get; init; } = 32f;

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        System.ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var ring = theme.SurfaceColor(context.UseSurface());
        var shown = Names.Count > Max ? Max - 1 : Names.Count;
        var overlap = Size * 0.3f;
        var children = new List<Element?>();
        Element Ringed(Element avatar, int i) => new Box
        {
            Layout = new LayoutStyle { Width = Size + 4, Height = Size + 4, AlignItems = Align.Center, JustifyContent = Justify.Center, Margin = new Edges(i == 0 ? 0 : -overlap, 0, 0, 0) },
            Background = ring,
            CornerRadii = Radiant.Graphics2D.CornerRadii.All((Size + 4) / 2),
            Children = [avatar],
        };
        for (var i = 0; i < shown; i++)
        {
            children.Add(Ringed(new Avatar(Names[i]) { Size = Size }, i));
        }
        if (shown < Names.Count)
        {
            children.Add(Ringed(new Surface
            {
                SurfaceColor = SurfaceName.SurfaceContainerHighest,
                CornerShape = CornerShapeRole.Full,
                Layout = new LayoutStyle { Width = Size, Height = Size, AlignItems = Align.Center, JustifyContent = Justify.Center },
                Children = [new SurfaceText("+" + (Names.Count - shown).ToString(CultureInfo.CurrentCulture)) { TextType = TextType.LabelMedium }],
            }, shown));
        }
        return new Box
        {
            Semantics = new Semantics { Role = SemanticsRole.Group, Label = string.Join(", ", Names) },
            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row },
            Children = children,
        };
    }
}
