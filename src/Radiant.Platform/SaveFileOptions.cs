using System.Collections.Generic;

namespace Radiant.Platform;

/// <summary>What a save panel asks for.</summary>
public sealed record SaveFileOptions
{
    /// <summary>The panel's title or prompt, or null for the platform's default.</summary>
    public string? Title { get; init; }

    /// <summary>The file name the panel suggests, such as "Untitled.txt".</summary>
    public string? SuggestedName { get; init; }

    /// <summary>The kinds of file the user may save as; empty allows any name.</summary>
    public IReadOnlyList<FileFilter> Filters { get; init; } = [];

    /// <summary>The folder the panel starts in, or null for the platform's choice.</summary>
    public string? InitialDirectory { get; init; }

    /// <summary>Whether the panel offers a New Folder button.</summary>
    public bool CanCreateDirectories { get; init; } = true;
}
