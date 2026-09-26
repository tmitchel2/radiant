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
    public int? ShowContextMenu(IReadOnlyList<PlatformMenuItem> items, Vector2 position)
    {
        LastShown = items;
        return OnShow?.Invoke(items, position);
    }
}
