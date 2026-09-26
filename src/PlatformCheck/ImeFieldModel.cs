using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using Radiant.Platform;
using Radiant.UI.Core;

namespace Radiant.PlatformCheck;

/// <summary>
/// The text of the check's input field and its composition, as the platform's text input
/// client: committed text, then the marked (provisional) text after it. A sketch of what the
/// real text field will do, just enough to watch an input method work.
/// </summary>
internal sealed class ImeFieldModel : ITextInputClient
{
    private const int LogLength = 8;
    private readonly Signal<int> _version = new(0);
    private readonly List<string> _log = [];

    /// <summary>Changes on every edit, for the field to watch.</summary>
    public IReadable<int> Version => _version;

    /// <summary>The committed text.</summary>
    public string Text { get; private set; } = "";

    /// <summary>The composition, shown after the text; empty when not composing.</summary>
    public string Marked { get; private set; } = "";

    /// <summary>The input method's selection within <see cref="Marked"/>.</summary>
    public (int Start, int Length) MarkedSelection { get; private set; }

    /// <summary>The latest calls from the platform, newest last.</summary>
    public IReadOnlyList<string> Log => _log;

    /// <summary>The committed text's box: its right edge is where a composition starts.</summary>
    public ElementRef TextRef { get; } = new();

    /// <summary>
    /// The start of the composition (the end of the committed text), so the candidate window
    /// sits under what's being composed and doesn't move while typing.
    /// </summary>
    public RectangleF CaretRect
    {
        get
        {
            var bounds = TextRef.Bounds;
            return new RectangleF(bounds.Right, bounds.Top, 1, Math.Max(bounds.Height, 20));
        }
    }

    /// <inheritdoc/>
    public void InsertText(string text)
    {
        Text += text;
        Marked = "";
        Changed($"insert \"{text}\"");
    }

    /// <inheritdoc/>
    public void SetMarkedText(string text, int selectionStart, int selectionLength)
    {
        Marked = text;
        MarkedSelection = (selectionStart, selectionLength);
        Changed(text.Length == 0 ? "cancel composition" : $"marked \"{text}\" selection {selectionStart}+{selectionLength}");
    }

    /// <inheritdoc/>
    public void UnmarkText()
    {
        Text += Marked;
        Marked = "";
        Changed("unmark");
    }

    /// <summary>Deletes the last character the user sees (a whole emoji or accented letter).</summary>
    public void Backspace()
    {
        if (Text.Length == 0)
        {
            return;
        }
        var starts = StringInfo.ParseCombiningCharacters(Text);
        Text = Text[..starts[^1]];
        Changed("backspace");
    }

    /// <summary>Replaces the text, as a paste over everything would.</summary>
    public void Replace(string text)
    {
        Text = text;
        Marked = "";
        Changed("replace");
    }

    /// <summary>Logs a key the UI received, to show which keys an input method kept to itself.</summary>
    public void KeyReceived(string key) => Changed($"key {key}");

    private void Changed(string entry)
    {
        _log.Add(entry);
        if (_log.Count > LogLength)
        {
            _log.RemoveAt(0);
        }
        _version.Value++;
    }
}
