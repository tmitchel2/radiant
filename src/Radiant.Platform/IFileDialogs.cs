using System.Collections.Generic;
using System.Threading.Tasks;

namespace Radiant.Platform;

/// <summary>
/// The system's open and save panels.
/// <para>
/// Both are asynchronous: the task completes, on the UI thread, when the user closes the panel.
/// A platform shows the panel attached to the window (a sheet on macOS) where it can, so the app
/// keeps drawing while the panel is open; without a window it shows an app-modal panel and the
/// task is already complete when returned. Call from the UI thread.
/// </para>
/// </summary>
public interface IFileDialogs
{
    /// <summary>Asks the user for files or folders to open. Empty if they cancelled.</summary>
    Task<IReadOnlyList<string>> OpenAsync(OpenFileOptions? options = null);

    /// <summary>Asks the user where to save. Null if they cancelled.</summary>
    Task<string?> SaveAsync(SaveFileOptions? options = null);
}
