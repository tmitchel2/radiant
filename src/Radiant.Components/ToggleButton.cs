using System;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// An icon button that stays on or off (bold, pin, a view mode): filled with the secondary container
/// while on, its icon filled too. A button to assistive technology, named by its label and
/// selected while on.
/// Controlled: shows <paramref name="On"/> and reports each press with the new state.
/// </summary>
/// <param name="Icon">The icon.</param>
/// <param name="Label">What it toggles.</param>
/// <param name="On">Whether it's on.</param>
/// <param name="OnChange">Called with the new state.</param>
[RequiresTestId]
public sealed record ToggleButton(string Icon, string Label, bool On, Action<bool>? OnChange) : Component
{
    /// <summary>Tab order: 0 in tree order, negative for one of a roving group's other buttons.</summary>
    public int TabIndex { get; init; }

    /// <summary>Whether it can't be used.</summary>
    public bool Disabled { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        var (on, change) = (On, OnChange);
        return new PressableSurface
        {
            SurfaceColor = On ? SurfaceName.Secondary : null,
            SurfaceContainerToggle = On ? true : null,
            ShowDisabled = Disabled ? true : null,
            CornerShape = CornerShapeRole.Small,
            Label = Label,
            TabIndex = TabIndex,
            Selected = On,
            OnPress = () => change?.Invoke(!on),
            Layout = new LayoutStyle { Width = 36, Height = 36, AlignItems = Align.Center, JustifyContent = Justify.Center },
            Children = [new SurfaceIcon(Icon) { IconSize = 20, IconFilled = On, Legibility = On ? null : Legibility.Medium }],
        };
    }
}
