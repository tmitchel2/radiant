namespace Radiant.Platform;

/// <summary>
/// The native handles of the window a platform serves, from whatever opened it (GLFW, through
/// <c>RadiantApplication</c>). Zero where a handle doesn't apply or the window isn't open.
/// </summary>
public readonly record struct NativeWindow
{
    /// <summary>The window's <c>NSWindow*</c> on macOS.</summary>
    public nint Cocoa { get; init; }

    /// <summary>The window's <c>GLFWwindow*</c>.</summary>
    public nint Glfw { get; init; }
}
