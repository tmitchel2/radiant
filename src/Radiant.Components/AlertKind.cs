namespace Radiant.Components;

/// <summary>What an <see cref="Alert"/> is about: its colour and icon.</summary>
public enum AlertKind
{
    /// <summary>Something to know.</summary>
    Info,

    /// <summary>Something went well.</summary>
    Success,

    /// <summary>Something to be careful of.</summary>
    Warning,

    /// <summary>Something went wrong.</summary>
    Error,
}
