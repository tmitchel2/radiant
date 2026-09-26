using Radiant.Components;
using Radiant.Layout;
using Radiant.Platform;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.PlatformCheck;

/// <summary>A tile showing its cursor's name, with that cursor while the pointer is over it.</summary>
internal sealed record CursorSwatch(CursorShape Shape) : Component
{
    public override Element? Build(BuildContext context) => new Box
    {
        Cursor = Shape,
        Children =
        [
            new Card(new SurfaceText(Shape.ToString()) { TextType = TextType.LabelLarge })
            {
                Variant = CardVariant.Filled,
                SurfaceColor = SurfaceName.Secondary,
                SurfaceContainerToggle = true,
                Layout = new LayoutStyle { Padding = new Edges(12, 8, 12, 8), MinWidth = 120 },
            },
        ],
    };
}
