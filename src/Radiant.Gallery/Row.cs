using System.Collections.Generic;
using Radiant.Layout;
using Radiant.UI.Core;

namespace Radiant.Gallery;

/// <summary>Children side by side, wrapping, with a gap.</summary>
internal sealed record Row(params Element?[] Items) : Component
{
    public float Gap { get; init; } = 8f;

    public override Element? Build(BuildContext context) => new Box
    {
        Layout = new LayoutStyle
        {
            FlexDirection = FlexDirection.Row,
            FlexWrap = FlexWrap.Wrap,
            AlignItems = Align.Center,
            ColumnGap = Gap,
            RowGap = Gap,
        },
        Children = (IReadOnlyList<Element?>)Items,
    };
}
