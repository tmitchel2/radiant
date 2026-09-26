using System;

namespace Radiant.UI.Core;

/// <summary>A command in a <see cref="CommandRegistry"/>: update it in place, or dispose it to remove it.</summary>
public abstract class CommandRegistration : IDisposable
{
    /// <summary>Replaces the registered command, keeping its place.</summary>
    public abstract void Update(Command command);

    /// <inheritdoc/>
    public abstract void Dispose();
}
