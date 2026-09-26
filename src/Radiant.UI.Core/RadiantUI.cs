using System;
using System.Numerics;
using Radiant.Graphics;
using Radiant.Input;

namespace Radiant.UI.Core;

/// <summary>Runs a UI in a window.</summary>
public static class RadiantUI
{
    /// <summary>
    /// Opens a window showing <paramref name="root"/> and runs until it closes: window input is
    /// routed to the tree as events, and each frame the tree is updated, laid out to the window
    /// and drawn.
    /// </summary>
    public static void Run(Element root, UIAppOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        options ??= new UIAppOptions();
        using var app = new RadiantApplication();
        using var ui = new UIRoot(root, options.Fonts);

        app.PointerMoved += position => ui.PointerMove(position, Modifiers(app.Input));
        app.PointerPressed += button => ui.PointerDown(app.Input.MousePosition, (PointerButton)(int)button, Modifiers(app.Input));
        app.PointerReleased += button => ui.PointerUp(app.Input.MousePosition, (PointerButton)(int)button, Modifiers(app.Input));
        app.Scrolled += offset => ui.Wheel(app.Input.MousePosition, -offset * options.WheelStep, Modifiers(app.Input));
        app.KeyPressed += key => ui.KeyDown((KeyCode)(int)key, Modifiers(app.Input));
        app.KeyReleased += key => ui.KeyUp((KeyCode)(int)key, Modifiers(app.Input));
        app.CharacterTyped += character => ui.TextInput(character.ToString());

        app.Run(options.Title, options.Width, options.Height, Handedness.RightHanded, renderer =>
        {
            ui.Update(new Vector2(app.WindowWidth, app.WindowHeight));
            ui.Paint(renderer);
        }, updateCallback: null, options.Background);
    }

    private static KeyModifiers Modifiers(InputState input)
    {
        var modifiers = KeyModifiers.None;
        if (input.IsShiftDown)
        {
            modifiers |= KeyModifiers.Shift;
        }
        if (input.IsCtrlDown)
        {
            modifiers |= KeyModifiers.Control;
        }
        if (input.IsAltDown)
        {
            modifiers |= KeyModifiers.Alt;
        }
        if (input.IsKeyDown(Silk.NET.Input.Key.SuperLeft) || input.IsKeyDown(Silk.NET.Input.Key.SuperRight))
        {
            modifiers |= KeyModifiers.Super;
        }
        return modifiers;
    }
}
