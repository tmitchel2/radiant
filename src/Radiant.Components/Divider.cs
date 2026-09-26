using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>A thin line separating content, in the quiet outline colour.</summary>
public sealed record Divider : Component
{
    /// <summary>Whether it runs top to bottom (between columns) rather than across.</summary>
    public bool Vertical { get; init; }

    /// <summary>Space before it, along its length (an inset list divider).</summary>
    public float Inset { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        System.ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        return new Box
        {
            Background = theme.OutlineVariant,
            Layout = Vertical
                ? new LayoutStyle { Width = 1, AlignSelf = Align.Stretch, Margin = new Edges(0, Inset, 0, 0) }
                : new LayoutStyle { Height = 1, AlignSelf = Align.Stretch, Margin = new Edges(Inset, 0, 0, 0) },
        };
    }
}
