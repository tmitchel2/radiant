using System;
using System.Collections.Generic;
using System.Numerics;
using Radiant.Platform;
using Radiant.Text;

namespace Radiant.UI.Core;

/// <summary>How <see cref="RadiantUI.Run"/> opens its window.</summary>
public sealed record UIAppOptions
{
    /// <summary>The window title.</summary>
    public string Title { get; init; } = "Radiant";

    /// <summary>The window's width, in logical pixels.</summary>
    public int Width { get; init; } = 1024;

    /// <summary>The window's height, in logical pixels.</summary>
    public int Height { get; init; } = 768;

    /// <summary>What shows behind the UI, linear and straight alpha.</summary>
    public Vector4 Background { get; init; } = Vector4.One;

    /// <summary>The fonts text is set in; Radiant's embedded fonts if null.</summary>
    public FontLibrary? Fonts { get; init; }

    /// <summary>How many pixels one notch of a mouse wheel scrolls.</summary>
    public float WheelStep { get; init; } = 40f;

    /// <summary>
    /// Makes the platform for the window once it's open, such as
    /// <c>MacPlatform.CreateOrHeadless</c> from Radiant.Platform.MacOS. Null runs headless: no
    /// system clipboard, cursors, dialogs or input methods. The UI can't pick one itself, as it
    /// mustn't depend on any operating system's implementation.
    /// </summary>
    public Func<NativeWindow, IPlatform>? Platform { get; init; }

    /// <summary>
    /// Whether to run without a window: frames are run by a loop on the calling thread, on the
    /// headless platform, until <see cref="UIAppSession.Exit"/>. For automated tests and agents.
    /// </summary>
    public bool Headless { get; init; }

    /// <summary>How the app tells time; <see cref="UIClockMode.Fixed"/> makes runs repeatable.</summary>
    public UIClockMode Clock { get; init; } = UIClockMode.Real;

    /// <summary>Pixels per logical pixel when headless (a window uses its display's); 1 if null.</summary>
    public float? PixelScale { get; init; }

    /// <summary>What plugs into the app once it's running: an automation server, a recorder.</summary>
    public IReadOnlyList<IUIAppExtension> Extensions { get; init; } = [];
}
