namespace Radiant.UI.Core;

/// <summary>Contexts text inputs read.</summary>
public static class TextInputContexts
{
    /// <summary>The clipboard; a process-private one where the app provides none.</summary>
    public static Context<ITextClipboard> Clipboard { get; } = new(new MemoryClipboard());
}
