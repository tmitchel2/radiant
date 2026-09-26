using System;
using System.Collections.Generic;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// The snackbars waiting to be shown, one at a time in order. Reach it from a component with
/// <c>context.UseSnackbars()</c> (under a <see cref="SnackbarHost"/>) and call <see cref="Show(string, string?, Action?)"/>.
/// </summary>
public sealed class Snackbars
{
    private readonly Queue<SnackbarMessage> _waiting = new();
    private readonly Signal<SnackbarMessage?> _current = new(null);

    /// <summary>The message showing, or null.</summary>
    public IReadable<SnackbarMessage?> Current => _current;

    /// <summary>Queues a message.</summary>
    public void Show(SnackbarMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (_current.Value is null)
        {
            _current.Value = message;
        }
        else
        {
            _waiting.Enqueue(message);
        }
    }

    /// <summary>Queues a message with an optional action.</summary>
    public void Show(string text, string? actionLabel = null, Action? onAction = null) =>
        Show(new SnackbarMessage(text) { ActionLabel = actionLabel, OnAction = onAction });

    /// <summary>Takes the current message away and shows the next, if any.</summary>
    public void Dismiss() => _current.Value = _waiting.Count > 0 ? _waiting.Dequeue() : null;
}
