using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Radiant.Platform.MacOS;

/// <summary>
/// <c>NSOpenPanel</c> and <c>NSSavePanel</c>. With a window, the panel is a sheet on it
/// (<c>beginSheetModalForWindow:completionHandler:</c>): the app keeps drawing, and the task
/// completes on the main thread when the user closes the sheet. Without one it's app-modal
/// (<c>runModal</c>), which blocks until the user answers.
/// <para>
/// Filters become the panel's allowed content types (<c>UTType</c>s from each extension, or
/// plain extensions before macOS 11). macOS has no filter menu, so every filter's files are
/// allowed at once.
/// </para>
/// </summary>
internal sealed class MacFileDialogs(nint window) : IFileDialogs
{
    private const nint ModalResponseOK = 1;

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> OpenAsync(OpenFileOptions? options = null)
    {
        options ??= new OpenFileOptions();
        RequireMainThread();
        using var pool = ObjC.Pool();
        var panel = ObjC.Send(ObjC.Class("NSOpenPanel"), "openPanel");
        ObjC.SendBool(panel, "setCanChooseFiles:", options.CanChooseFiles);
        ObjC.SendBool(panel, "setCanChooseDirectories:", options.CanChooseDirectories);
        ObjC.SendBool(panel, "setAllowsMultipleSelection:", options.AllowMultiple);
        Configure(panel, options.Title, options.Filters, options.InitialDirectory);
        return Show(panel, ok => ok ? Paths(panel) : (IReadOnlyList<string>)[]);
    }

    /// <inheritdoc/>
    public Task<string?> SaveAsync(SaveFileOptions? options = null)
    {
        options ??= new SaveFileOptions();
        RequireMainThread();
        using var pool = ObjC.Pool();
        var panel = ObjC.Send(ObjC.Class("NSSavePanel"), "savePanel");
        ObjC.SendBool(panel, "setCanCreateDirectories:", options.CanCreateDirectories);
        if (options.SuggestedName is { } name)
        {
            ObjC.Send(panel, "setNameFieldStringValue:", ObjC.String(name));
        }
        Configure(panel, options.Title, options.Filters, options.InitialDirectory);
        return Show(panel, ok => ok ? Path(ObjC.Send(panel, "URL")) : null);
    }

    private static void Configure(nint panel, string? title, IReadOnlyList<FileFilter> filters, string? directory)
    {
        if (title is not null)
        {
            // A sheet has no title bar; the message is what shows.
            ObjC.Send(panel, "setTitle:", ObjC.String(title));
            ObjC.Send(panel, "setMessage:", ObjC.String(title));
        }
        var extensions = filters.SelectMany(f => f.Extensions).Select(e => e.TrimStart('.')).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (extensions.Length > 0)
        {
            var utType = ObjC.Class("UTType");
            if (utType != 0 && ObjC.RespondsTo(panel, "setAllowedContentTypes:"))
            {
                var types = ObjC.NSArray();
                foreach (var extension in extensions)
                {
                    var type = ObjC.Send(utType, "typeWithFilenameExtension:", ObjC.String(extension));
                    if (type != 0)
                    {
                        ObjC.Send(types, "addObject:", type);
                    }
                }
                ObjC.Send(panel, "setAllowedContentTypes:", types);
            }
            else
            {
                ObjC.Send(panel, "setAllowedFileTypes:", ObjC.NSArray([.. extensions.Select(ObjC.String)]));
            }
        }
        if (directory is not null)
        {
            var url = ObjC.Send(ObjC.Class("NSURL"), "fileURLWithPath:", ObjC.String(directory));
            ObjC.Send(panel, "setDirectoryURL:", url);
        }
    }

    private Task<T> Show<T>(nint panel, Func<bool, T> result)
    {
        if (window == 0)
        {
            return Task.FromResult(result(ObjC.Send(panel, "runModal") == ModalResponseOK));
        }
        // Completion runs on the main thread from the event loop. Continuations run inline there
        // (no RunContinuationsAsynchronously), so an awaiting UI handler resumes on the UI thread.
        var completion = new TaskCompletionSource<T>();
        ObjC.Send(panel, "retain");
        using var block = new CompletionBlock(response =>
        {
            using var pool = ObjC.Pool();
            try
            {
                completion.SetResult(result(response == ModalResponseOK));
            }
            finally
            {
                ObjC.Send(panel, "release");
            }
        });
        ObjC.Send(panel, "beginSheetModalForWindow:completionHandler:", window, block.Pointer);
        return completion.Task;
    }

    private static List<string> Paths(nint panel)
    {
        var urls = ObjC.Send(panel, "URLs");
        var count = (int)ObjC.Send(urls, "count");
        var paths = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            if (Path(ObjC.Send(urls, "objectAtIndex:", i)) is { } path)
            {
                paths.Add(path);
            }
        }
        return paths;
    }

    private static string? Path(nint url) => url == 0 ? null : ObjC.ToManagedString(ObjC.Send(url, "path"));

    private static void RequireMainThread()
    {
        if (!ObjC.GetBool(ObjC.Class("NSThread"), "isMainThread"))
        {
            throw new InvalidOperationException("File dialogs must be shown from the UI (main) thread.");
        }
    }
}
