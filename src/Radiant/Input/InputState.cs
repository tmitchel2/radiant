using System.Collections.Generic;
using System.Numerics;
using Silk.NET.Input;

namespace Radiant.Input;

/// <summary>
/// Tracks the current state of input devices (mouse, keyboard).
/// </summary>
public class InputState
{
    private readonly HashSet<MouseButton> _mouseButtonsDown = [];
    private readonly HashSet<MouseButton> _mouseButtonsPressed = [];
    private readonly HashSet<MouseButton> _mouseButtonsReleased = [];
    private readonly HashSet<Key> _keysDown = [];
    private readonly HashSet<Key> _keysPressed = [];
    private readonly HashSet<Key> _keysReleased = [];

    /// <summary>Current mouse position in window coordinates.</summary>
    public Vector2 MousePosition { get; set; }

    /// <summary>Mouse movement delta since last frame.</summary>
    public Vector2 MouseDelta { get; set; }

    /// <summary>Scroll wheel delta since last frame.</summary>
    public Vector2 ScrollDelta { get; set; }

    /// <summary>Last character typed (for text input).</summary>
    public char? LastCharacter { get; set; }

    /// <summary>Returns true if the mouse button is currently held down.</summary>
    public bool IsMouseButtonDown(MouseButton button) => _mouseButtonsDown.Contains(button);

    /// <summary>Returns true if the mouse button was pressed this frame.</summary>
    public bool IsMouseButtonPressed(MouseButton button) => _mouseButtonsPressed.Contains(button);

    /// <summary>Returns true if the mouse button was released this frame.</summary>
    public bool IsMouseButtonReleased(MouseButton button) => _mouseButtonsReleased.Contains(button);

    /// <summary>Returns true if the left mouse button is down.</summary>
    public bool IsLeftMouseDown => IsMouseButtonDown(MouseButton.Left);

    /// <summary>Returns true if the right mouse button is down.</summary>
    public bool IsRightMouseDown => IsMouseButtonDown(MouseButton.Right);

    /// <summary>Returns true if the middle mouse button is down.</summary>
    public bool IsMiddleMouseDown => IsMouseButtonDown(MouseButton.Middle);

    /// <summary>Returns true if the key is currently held down.</summary>
    public bool IsKeyDown(Key key) => _keysDown.Contains(key);

    /// <summary>Returns true if the key was pressed this frame.</summary>
    public bool IsKeyPressed(Key key) => _keysPressed.Contains(key);

    /// <summary>Returns true if the key was released this frame.</summary>
    public bool IsKeyReleased(Key key) => _keysReleased.Contains(key);

    /// <summary>Returns true if Shift is held.</summary>
    public bool IsShiftDown => IsKeyDown(Key.ShiftLeft) || IsKeyDown(Key.ShiftRight);

    /// <summary>Returns true if Ctrl is held.</summary>
    public bool IsCtrlDown => IsKeyDown(Key.ControlLeft) || IsKeyDown(Key.ControlRight);

    /// <summary>Returns true if Alt is held.</summary>
    public bool IsAltDown => IsKeyDown(Key.AltLeft) || IsKeyDown(Key.AltRight);

    public void SetMouseButton(MouseButton button, bool down)
    {
        if (down)
        {
            if (_mouseButtonsDown.Add(button))
            {
                _mouseButtonsPressed.Add(button);
            }
        }
        else
        {
            if (_mouseButtonsDown.Remove(button))
            {
                _mouseButtonsReleased.Add(button);
            }
        }
    }

    public void SetKey(Key key, bool down)
    {
        if (down)
        {
            if (_keysDown.Add(key))
            {
                _keysPressed.Add(key);
            }
        }
        else
        {
            if (_keysDown.Remove(key))
            {
                _keysReleased.Add(key);
            }
        }
    }

    /// <summary>Clears per-frame state (pressed, released, deltas).</summary>
    public void EndFrame()
    {
        _mouseButtonsPressed.Clear();
        _mouseButtonsReleased.Clear();
        _keysPressed.Clear();
        _keysReleased.Clear();
        MouseDelta = Vector2.Zero;
        ScrollDelta = Vector2.Zero;
        LastCharacter = null;
    }
}
