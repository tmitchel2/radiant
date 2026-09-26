using System;
using Radiant.Platform;

namespace Radiant.UI.Core;

/// <summary>
/// The operating system's services (clipboard, file dialogs, appearance, cursors, text input)
/// as a context, so any component can reach them: <c>context.UsePlatform().Clipboard</c>.
/// <see cref="RadiantUI.Run"/> provides the window's platform above the app. Where nothing
/// provides one (tests, offscreen snapshots) it's a <see cref="HeadlessPlatform"/>.
/// </summary>
public static class PlatformContext
{
    /// <summary>The context; provide a platform with <c>PlatformContext.Platform.Provide(platform, child)</c>.</summary>
    public static Context<IPlatform> Platform { get; } = new(new HeadlessPlatform());

    /// <summary>The platform above this component.</summary>
    public static IPlatform UsePlatform(this BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Use(Platform);
    }
}
