using System;

namespace Radiant.UI.Core;

/// <summary>
/// The base of every UI event. Events travel from the root down to their target (handlers named
/// <c>…Capture</c>), then back up (the others); a handler that sets <see cref="Handled"/> stops it.
/// </summary>
public abstract class UIEventArgs : EventArgs
{
    /// <summary>Whether a handler has dealt with the event, which stops it travelling further.</summary>
    public bool Handled { get; set; }
}
