using System;

namespace Radiant.UI.Core;

/// <summary>The modifier keys held during an input event.</summary>
[Flags]
public enum KeyModifiers
{
    /// <summary>None held.</summary>
    None = 0,

    /// <summary>Shift.</summary>
    Shift = 1,

    /// <summary>Control.</summary>
    Control = 2,

    /// <summary>Alt, or Option on macOS.</summary>
    Alt = 4,

    /// <summary>Command on macOS, the Windows key elsewhere.</summary>
    Super = 8,
}
