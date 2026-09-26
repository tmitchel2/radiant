using System;
using Radiant.Text;

namespace Radiant.UI.Core;

/// <summary>The direction hooks.</summary>
public static class DirectionalityHooks
{
    /// <summary>The direction set by the nearest <see cref="Directionality"/>, or null where none is.</summary>
    public static TextDirection? UseDirection(this BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Use(Directionality.Context);
    }

    /// <summary>Whether things here read right to left, so "forward" is leftwards.</summary>
    public static bool UseRightToLeft(this BuildContext context) => context.UseDirection() == TextDirection.RightToLeft;
}
