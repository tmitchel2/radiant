using System.Collections.Generic;
using System.Numerics;

namespace Radiant.UI.Core;

/// <summary>Files dropped on the window, sent to the box under the pointer and its ancestors.</summary>
public sealed class FileDropEventArgs : UIEventArgs
{
    internal FileDropEventArgs(Vector2 position, IReadOnlyList<string> paths)
    {
        Position = position;
        Paths = paths;
    }

    /// <summary>Where they were dropped, in the root's coordinates.</summary>
    public Vector2 Position { get; }

    /// <summary>The dropped files' (and folders') full paths.</summary>
    public IReadOnlyList<string> Paths { get; }
}
