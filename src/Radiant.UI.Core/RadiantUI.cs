using System;
using System.Numerics;
using Radiant.Graphics;
using Radiant.Input;
using Radiant.Platform;

namespace Radiant.UI.Core;

/// <summary>Runs a UI in a window, or headless.</summary>
public static class RadiantUI
{
    /// <summary>
    /// Opens a window showing <paramref name="root"/> and runs until it closes: window input is
    /// routed to the tree as events, and each frame the tree is updated, laid out to the window
    /// and drawn. With <see cref="UIAppOptions.Headless"/>, runs without a window instead
    /// (<see cref="RunHeadless"/>).
    /// </summary>
    public static void Run(Element root, UIAppOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        options ??= new UIAppOptions();
        if (options.Headless)
        {
            RunHeadless(root, options);
            return;
        }
        using var app = new RadiantApplication();
        using var session = new UIAppSession(root, options);
        var ui = session.Root;
        session.WakeUp = app.RequestFrame;
        session.CloseRequested = app.Close;

        // The platform needs the native window, so it's made once the window is open; the tree
        // isn't mounted until the first frame, after this.
        app.Loaded += () =>
        {
            var created = options.Platform?.Invoke(new NativeWindow { Cocoa = app.CocoaWindow, Glfw = app.GlfwWindow })
                ?? new HeadlessPlatform();
            UpdateWindow(app, session);
            session.AttachPlatform(created);
        };

        // Frames are drawn only while the tree (or an extension) has something to do; otherwise the window waits.
        app.NeedsFrame = () => session.NeedsFrame;
        ui.FrameRequested = app.RequestFrame;

        app.PointerMoved += position => ui.PointerMove(position, Modifiers(app.Input));
        app.PointerPressed += button => ui.PointerDown(app.Input.MousePosition, (PointerButton)(int)button, Modifiers(app.Input));
        app.PointerReleased += button => ui.PointerUp(app.Input.MousePosition, (PointerButton)(int)button, Modifiers(app.Input));
        // The platform reports wheel notches, up and left positive; the UI takes pixels towards the content's end.
        app.Scrolled += offset => ui.Wheel(app.Input.MousePosition, -offset * options.WheelStep, Modifiers(app.Input));
        app.KeyPressed += key => ui.KeyDown((KeyCode)(int)key, Modifiers(app.Input));
        app.KeyReleased += key => ui.KeyUp((KeyCode)(int)key, Modifiers(app.Input));
        app.CharacterTyped += character => ui.TextInput(character.ToString());
        app.FilesDropped += paths => ui.DropFiles(app.Input.MousePosition, paths);

        app.Run(options.Title, options.Width, options.Height, Handedness.RightHanded, renderer =>
        {
            UpdateWindow(app, session);
            session.EndFrame();
            session.Paint(renderer);
        }, seconds => session.BeginFrame(session.NextStep(seconds)), options.Background);
    }

    /// <summary>
    /// Runs <paramref name="root"/> with no window, on the calling thread, until
    /// <see cref="UIAppSession.Exit"/>: frames at <see cref="UIAppOptions.Width"/> ×
    /// <see cref="UIAppOptions.Height"/> on the headless platform, only while there's something to do.
    /// On the real clock that's while the tree needs frames (at most 60 a second); on the fixed clock
    /// it's as fast as frames can be run while it's busy or an extension is waiting, and not at all
    /// otherwise, so spinners and timers wait where they are until something moves time on.
    /// </summary>
    public static void RunHeadless(Element root, UIAppOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        options = (options ?? new UIAppOptions()) with { Headless = true };
        // Made first so it's disposed last: the session's extensions may still ask for a frame as they go.
        using var wake = new System.Threading.AutoResetEvent(false);
        using var session = new UIAppSession(root, options);
        session.WakeUp = () => wake.Set();
        session.CloseRequested = () => wake.Set();
        session.Root.FrameRequested = session.RequestFrame;
        session.AttachPlatform(new HeadlessPlatform());

        var clock = System.Diagnostics.Stopwatch.StartNew();
        var last = 0.0;
        while (!session.ExitRequested)
        {
            var fixedClock = session.ClockMode == UIClockMode.Fixed;
            if (fixedClock ? session.HasWork : session.NeedsFrame)
            {
                var now = clock.Elapsed.TotalSeconds;
                session.RunFrame(session.NextStep(now - last));
                last = now;
                if (!fixedClock)
                {
                    // Sixty frames a second at most, as a display would allow.
                    var spare = UIAppSession.FixedStep - (clock.Elapsed.TotalSeconds - now);
                    if (spare > 0)
                    {
                        wake.WaitOne(TimeSpan.FromSeconds(spare));
                    }
                }
            }
            else
            {
                wake.WaitOne();
                // Time spent waiting isn't a frame's step.
                last = clock.Elapsed.TotalSeconds;
            }
        }
    }

    private static void UpdateWindow(RadiantApplication app, UIAppSession session)
    {
        if (app.WindowWidth > 0)
        {
            session.Size = new Vector2(app.WindowWidth, app.WindowHeight);
            session.PixelScale = app.FramebufferWidth / (float)app.WindowWidth;
        }
        session.WindowPosition = new Vector2(app.WindowX, app.WindowY);
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
