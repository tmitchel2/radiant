using System;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>The navigator hook, and the context it's found through.</summary>
public static class NavigatorHooks
{
    /// <summary>The nearest <see cref="StackNavigator"/>'s navigator, or null outside one.</summary>
    public static Context<Navigator?> Context { get; } = new(null);

    /// <summary>The nearest <see cref="StackNavigator"/>'s navigator, to push and pop pages; null outside one.</summary>
    public static Navigator? UseNavigator(this BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Use(Context);
    }
}
