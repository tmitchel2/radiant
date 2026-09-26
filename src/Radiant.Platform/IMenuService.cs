using System;
using System.Collections.Generic;
using System.Numerics;

namespace Radiant.Platform;

/// <summary>
/// The platform's own menus: context menus that look native and can reach outside the window, and
/// the menu bar where the platform has one of its own (macOS's, at the top of the screen).
/// </summary>
public interface IMenuService
{
    /// <summary>Whether the platform shows its own menus; otherwise the UI draws them.</summary>
    bool IsSupported { get; }

    /// <summary>
    /// Shows <paramref name="items"/> as a context menu at <paramref name="position"/> (in the
    /// window's content, points from its top left) and waits for the user: the chosen item's index,
    /// or null if they dismissed it. Call it while handling the press that asked for it.
    /// </summary>
    int? ShowContextMenu(IReadOnlyList<PlatformMenuItem> items, Vector2 position);

    /// <summary>Whether the platform has a menu bar of its own for <see cref="SetMenuBar"/>; otherwise the UI draws one.</summary>
    bool HasMenuBar { get; }

    /// <summary>
    /// Puts <paramref name="menus"/> on the platform's menu bar, after the application's own menu,
    /// replacing what was there. When the user chooses an item, from the menu or by its shortcut,
    /// <paramref name="onChoose"/> is called with the menu's and the item's indexes. An empty list
    /// leaves only the application menu.
    /// </summary>
    void SetMenuBar(IReadOnlyList<PlatformMenu> menus, Action<int, int> onChoose);
}
