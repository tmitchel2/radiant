using System.Collections.Generic;
using System.Numerics;

namespace Radiant.UI.Core;

/// <summary>What kind of input a <see cref="UIInputEvent"/> is.</summary>
public enum UIInputType
{
    /// <summary><see cref="UIRoot.PointerMove"/>.</summary>
    PointerMove,

    /// <summary><see cref="UIRoot.PointerDown"/>.</summary>
    PointerDown,

    /// <summary><see cref="UIRoot.PointerUp"/>.</summary>
    PointerUp,

    /// <summary><see cref="UIRoot.Wheel"/>.</summary>
    Wheel,

    /// <summary><see cref="UIRoot.KeyDown"/>.</summary>
    KeyDown,

    /// <summary><see cref="UIRoot.KeyUp"/>.</summary>
    KeyUp,

    /// <summary><see cref="UIRoot.TextInput"/>, or text committed through the platform's text input.</summary>
    Text,

    /// <summary><see cref="UIRoot.DropFiles"/>.</summary>
    FileDrop,
}

/// <summary>
/// Input a <see cref="UIRoot"/> received (<see cref="UIRoot.InputReceived"/>), before it's dispatched:
/// for recording what someone did.
/// </summary>
/// <param name="Type">What kind of input.</param>
public sealed record UIInputEvent(UIInputType Type)
{
    /// <summary>Where, in root coordinates, for pointer input.</summary>
    public Vector2 Position { get; init; }

    /// <summary>The button, for a press or release.</summary>
    public PointerButton Button { get; init; }

    /// <summary>The key, for key input.</summary>
    public KeyCode Key { get; init; }

    /// <summary>The modifiers held.</summary>
    public KeyModifiers Modifiers { get; init; }

    /// <summary>Whether a key press is the key repeating.</summary>
    public bool IsRepeat { get; init; }

    /// <summary>The text, for text input.</summary>
    public string? Text { get; init; }

    /// <summary>How far, for the wheel.</summary>
    public Vector2 Delta { get; init; }

    /// <summary>The files, for a drop.</summary>
    public IReadOnlyList<string>? Paths { get; init; }

    /// <summary>
    /// The node it reached: the topmost node under the pointer, or for keys and text the focused node;
    /// 0 for none. See <see cref="UIRoot.FindNode"/>.
    /// </summary>
    public int TargetId { get; init; }
}
