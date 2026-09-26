using System;
using System.Numerics;
using Radiant.Graphics;
using Radiant.Input;
using Radiant.Platform;

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
        using var platform = new PlatformBinding(ui);

        // The platform needs the native window, so it's made once the window is open; the tree
        // isn't mounted until the first frame, after this.
        app.Loaded += () =>
        {
            var created = options.Platform?.Invoke(new NativeWindow { Cocoa = app.CocoaWindow, Glfw = app.GlfwWindow })
                ?? new HeadlessPlatform();
            platform.Attach(created);
            ui.SetRoot(PlatformContext.Platform.Provide(created, root));
        };

        app.PointerMoved += position => ui.PointerMove(position, Modifiers(app.Input));
        app.PointerPressed += button => ui.PointerDown(app.Input.MousePosition, (PointerButton)(int)button, Modifiers(app.Input));
        app.PointerReleased += button => ui.PointerUp(app.Input.MousePosition, (PointerButton)(int)button, Modifiers(app.Input));
        // The platform reports wheel notches, up and left positive; the UI takes pixels towards the content's end.
        app.Scrolled += offset => ui.Wheel(app.Input.MousePosition, -offset * options.WheelStep, Modifiers(app.Input));
        app.KeyPressed += key => ui.KeyDown((KeyCode)(int)key, Modifiers(app.Input));
        app.KeyReleased += key => ui.KeyUp((KeyCode)(int)key, Modifiers(app.Input));
        app.CharacterTyped += character => ui.TextInput(character.ToString());

        app.Run(options.Title, options.Width, options.Height, Handedness.RightHanded, renderer =>
        {
            ui.Update(new Vector2(app.WindowWidth, app.WindowHeight));
            platform.AfterUpdate();
            ui.Paint(renderer);
        }, ui.Advance, options.Background);
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
