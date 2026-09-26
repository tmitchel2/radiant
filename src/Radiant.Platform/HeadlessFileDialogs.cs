using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Radiant.Platform;

/// <summary>
/// Dialogs that answer at once, as scripted by <see cref="OnOpen"/> and <see cref="OnSave"/>;
/// unscripted, the user always cancels.
/// </summary>
public sealed class HeadlessFileDialogs : IFileDialogs
{
    /// <summary>What the user picks in an open panel with the given options.</summary>
    public Func<OpenFileOptions, IReadOnlyList<string>>? OnOpen { get; set; }

    /// <summary>Where the user saves from a save panel with the given options.</summary>
    public Func<SaveFileOptions, string?>? OnSave { get; set; }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> OpenAsync(OpenFileOptions? options = null) =>
        Task.FromResult(OnOpen?.Invoke(options ?? new OpenFileOptions()) ?? []);

    /// <inheritdoc/>
    public Task<string?> SaveAsync(SaveFileOptions? options = null) =>
        Task.FromResult(OnSave?.Invoke(options ?? new SaveFileOptions()));
}
