using System;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>Hooks for showing snackbars.</summary>
public static class SnackbarHooks
{
    /// <summary>The nearest <see cref="SnackbarHost"/>'s queue.</summary>
    /// <exception cref="InvalidOperationException">No snackbar host is above this component.</exception>
    public static Snackbars UseSnackbars(this BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Use(SnackbarHost.Queue) ?? throw new InvalidOperationException("Snackbars need a SnackbarHost above the component.");
    }
}
