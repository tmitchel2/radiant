using System;
using System.Collections.Generic;
using System.Numerics;
using Radiant.Layout;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A menu of actions for what's under the pointer: a right-click (or Control-click on macOS) on
/// <paramref name="Child"/> opens <paramref name="Items"/> at the pointer; with focus inside,
/// Shift+F10 or the Menu key opens it at the child's top start corner. Where the platform has its own menus
/// (macOS), it's the platform's, which looks native and can reach outside the window; otherwise
/// it's a drawn <see cref="Menu"/>: arrows, a choice, Escape or a press outside.
/// </summary>
/// <param name="Child">What the menu is for.</param>
/// <param name="Items">The actions.</param>
public sealed record ContextMenu(Element? Child, IReadOnlyList<MenuItem> Items) : Component
{
    /// <summary>Called when the menu opens, before it shows (to update which items apply).</summary>
    public Action? OnOpen { get; init; }

    /// <summary>Whether to use the platform's own menu where it has one (the default); off always draws it.</summary>
    public bool PreferPlatformMenu { get; init; } = true;

    /// <summary>The area's size and placement.</summary>
    public LayoutStyle? Layout { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var area = context.UseRef(new ElementRef()).Value;
        var point = context.UseRef(new ElementRef()).Value;
        var at = context.UseState((Vector2?)null);
        var onOpen = OnOpen;
        var menus = context.UsePlatform().Menus;
        var rightToLeft = context.UseRightToLeft();
        var (items, native) = (Items, PreferPlatformMenu && menus.IsSupported);

        void OpenAt(Vector2 position)
        {
            onOpen?.Invoke();
            if (!native)
            {
                at.Set(position);
                return;
            }
            // The platform's menu runs until the user chooses; its entries map back to the items.
            var entries = new List<Radiant.Platform.PlatformMenuItem>();
            var owners = new List<int>();
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].DividerBefore && entries.Count > 0)
                {
                    entries.Add(Radiant.Platform.PlatformMenuItem.Separator);
                    owners.Add(-1);
                }
                entries.Add(new Radiant.Platform.PlatformMenuItem(items[i].Text) { Enabled = !items[i].Disabled });
                owners.Add(i);
            }
            if (menus.ShowContextMenu(entries, position) is { } chosen && chosen >= 0 && chosen < owners.Count && owners[chosen] >= 0)
            {
                items[owners[chosen]].OnSelect?.Invoke();
            }
        }

        return new Fragment(
            new Box
            {
                Ref = area,
                Layout = Layout ?? default,
                OnPointerDown = e =>
                {
                    var control = OperatingSystem.IsMacOS() && e.Button == PointerButton.Left && (e.Modifiers & KeyModifiers.Control) != 0;
                    if (e.Button == PointerButton.Right || control)
                    {
                        OpenAt(e.Position);
                        e.Handled = true;
                    }
                },
                OnKeyDown = e =>
                {
                    if (e.Key == KeyCode.Menu || e.Key == KeyCode.F10 && (e.Modifiers & KeyModifiers.Shift) != 0)
                    {
                        // At the child's top start corner.
                        var bounds = area.Bounds;
                        OpenAt(new Vector2(rightToLeft ? bounds.X + bounds.Width : bounds.X, bounds.Y));
                        e.Handled = true;
                    }
                },
                Children = [Child],
            },
            // The menu drops from a point: a 1 px box, in the root's coordinates, where it was asked for.
            at.Value is not { } position ? null : new Portal(new Box
            {
                Ref = point,
                HitTestVisible = false,
                Layout = new LayoutStyle
                {
                    Position = PositionType.Absolute,
                    Inset = Edges.Physical(position.X, position.Y, Dimension.Undefined, Dimension.Undefined, rightToLeft),
                    Width = 1,
                    Height = 1,
                },
            }),
            new Menu(point, at.Value is not null, () => at.Set(null), Items));
    }
}
