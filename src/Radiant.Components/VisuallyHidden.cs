using System;
using Radiant.Layout;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// Text for assistive technology only: read out, but not drawn and taking no space ("3 unread
/// messages" beside a badge that shows only "3").
/// </summary>
/// <param name="Text">What's read out.</param>
public sealed record VisuallyHidden(string Text) : Component
{
    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new Box
        {
            Semantics = new Semantics { Role = SemanticsRole.Text, Label = Text },
            HitTestVisible = false,
            ClipContent = true,
            Layout = new LayoutStyle { Position = PositionType.Absolute, Width = 1, Height = 1 },
        };
    }
}
