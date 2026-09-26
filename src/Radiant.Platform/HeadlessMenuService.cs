using System;
using System.Collections.Generic;
using System.Numerics;

namespace Radiant.Platform;

/// <summary>Platform menus without a platform: tests say which item the user would choose.</summary>
public sealed class HeadlessMenuService : IMenuService
{
    /// <inheritdoc/>
    public bool IsSupported { get; set; }

    /// <summary>Chooses from a menu shown with the given items at a position; dismissed (null) if not set.</summary>
    public Func<IReadOnlyList<PlatformMenuItem>, Vector2, int?>? OnShow { get; set; }

    /// <summary>The items of the last menu shown.</summary>
    public IReadOnlyList<PlatformMenuItem>? LastShown { get; private set; }

    /// <inheritdoc/>
    public bool HasMenuBar { get; set; }

    /// <summary>The menus last put on the menu bar.</summary>
    public IReadOnlyList<PlatformMenu> MenuBar { get; private set; } = [];

    private Action<int, int>? _onChoose;

    /// <inheritdoc/>
    public void SetMenuBar(IReadOnlyList<PlatformMenu> menus, Action<int, int> onChoose)
    {
        MenuBar = menus;
        _onChoose = onChoose;
    }

    /// <summary>Chooses an item from the menu bar, as the user would.</summary>
    public void ChooseFromMenuBar(int menu, int item) => _onChoose?.Invoke(menu, item);

    /// <inheritdoc/>
    public int? ShowContextMenu(IReadOnlyList<PlatformMenuItem> items, Vector2 position)
    {
        LastShown = items;
        return OnShow?.Invoke(items, position);
    }
}
