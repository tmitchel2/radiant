using System.Collections.Generic;
using System.Drawing;

namespace Radiant.Platform.MacOS.Tests;

/// <summary>A text input client that records what it was told.</summary>
internal sealed class RecordingClient : ITextInputClient
{
    public List<string> Calls { get; } = [];

    public RectangleF CaretRect { get; set; }

    public void InsertText(string text) => Calls.Add($"insert:{text}");

    public void SetMarkedText(string text, int selectionStart, int selectionLength) =>
        Calls.Add($"marked:{text}:{selectionStart}:{selectionLength}");

    public void UnmarkText() => Calls.Add("unmark");
}
