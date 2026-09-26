using System.Collections.Generic;

namespace Radiant.Platform;

/// <summary>What an open panel asks for.</summary>
public sealed record OpenFileOptions
{
    /// <summary>The panel's title or prompt, or null for the platform's default.</summary>
    public string? Title { get; init; }

    /// <summary>Whether the user can pick more than one item.</summary>
    public bool AllowMultiple { get; init; }

    /// <summary>Whether files can be picked.</summary>
    public bool CanChooseFiles { get; init; } = true;

    /// <summary>Whether folders can be picked.</summary>
    public bool CanChooseDirectories { get; init; }

    /// <summary>The kinds of file allowed; empty allows any file.</summary>
    public IReadOnlyList<FileFilter> Filters { get; init; } = [];

    /// <summary>The folder the panel starts in, or null for the platform's choice (usually the last one used).</summary>
    public string? InitialDirectory { get; init; }
}
