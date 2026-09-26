using System.Collections.Generic;
using System.Numerics;

namespace Radiant.Platform;

/// <summary>The platform's own menus: context menus that look native and can reach outside the window.</summary>
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
}
