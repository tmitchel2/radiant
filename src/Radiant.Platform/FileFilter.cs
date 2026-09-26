using System.Collections.Generic;

namespace Radiant.Platform;

/// <summary>
/// A kind of file a dialog offers, such as <c>new FileFilter("Images", ["png", "jpg"])</c>.
/// Extensions have no dot. macOS shows no filter menu: its panels allow the files of every filter
/// at once, so the name is used only where a platform lists filters.
/// </summary>
/// <param name="Name">What the user sees, such as "Images".</param>
/// <param name="Extensions">The file extensions, without dots.</param>
public sealed record FileFilter(string Name, IReadOnlyList<string> Extensions);
