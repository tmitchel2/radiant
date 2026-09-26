using System;

namespace Radiant.Platform;

/// <summary>The keys held with a menu item's shortcut.</summary>
[Flags]
public enum MenuModifiers
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>Shift.</summary>
    Shift = 1,

    /// <summary>Control (⌃ on macOS).</summary>
    Control = 2,

    /// <summary>Alt (⌥ Option on macOS).</summary>
    Alt = 4,

    /// <summary>The platform's command key: ⌘ on macOS.</summary>
    Command = 8,
}
