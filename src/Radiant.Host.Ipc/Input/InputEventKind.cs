namespace Radiant.Host.Ipc.Input;

/// <summary>
/// Kind of an input event carried host→renderer through an <see cref="InputRing"/>. Values are stable
/// on the wire so a host and a renderer built from different worktrees agree on interpretation.
/// </summary>
public enum InputEventKind
{
    /// <summary>A mouse button went down. Code = Silk.NET MouseButton int value.</summary>
    MouseDown = 0,

    /// <summary>A mouse button went up. Code = Silk.NET MouseButton int value.</summary>
    MouseUp = 1,

    /// <summary>Scroll wheel moved. X/Y carry the scroll delta.</summary>
    Scroll = 2,

    /// <summary>A key went down. Code = Silk.NET Key int value.</summary>
    KeyDown = 3,

    /// <summary>A key went up. Code = Silk.NET Key int value.</summary>
    KeyUp = 4,

    /// <summary>A character was typed. Code = UTF-16 code unit.</summary>
    Char = 5,
}
