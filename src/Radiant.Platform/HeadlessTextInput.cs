using System;

namespace Radiant.Platform;

/// <summary>
/// Text input driven by calls rather than a keyboard: <see cref="Type"/>, <see cref="Compose"/>
/// and <see cref="Unmark"/> act as an input method would on the focused client, so tests can
/// exercise composition without one.
/// </summary>
public sealed class HeadlessTextInput : ITextInput
{
    /// <inheritdoc/>
    public ITextInputClient? Client { get; private set; }

    /// <inheritdoc/>
    public bool IsComposing { get; private set; }

    /// <summary>How many times <see cref="InvalidateCaret"/> was called.</summary>
    public int CaretInvalidations { get; private set; }

    /// <inheritdoc/>
    public void Focus(ITextInputClient? client)
    {
        if (ReferenceEquals(client, Client))
        {
            return;
        }
        if (IsComposing)
        {
            IsComposing = false;
            Client?.UnmarkText();
        }
        Client = client;
    }

    /// <inheritdoc/>
    public void InvalidateCaret() => CaretInvalidations++;

    /// <summary>Commits <paramref name="text"/> to the client, as typing or an input method's conversion would.</summary>
    /// <returns>Whether a client took it.</returns>
    public bool Type(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (Client is not { } client)
        {
            return false;
        }
        IsComposing = false;
        client.InsertText(text);
        return true;
    }

    /// <summary>Shows provisional text in the client, as an input method does while composing; empty cancels.</summary>
    /// <returns>Whether a client took it.</returns>
    public bool Compose(string text, int selectionStart, int selectionLength = 0)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (Client is not { } client)
        {
            return false;
        }
        IsComposing = text.Length > 0;
        client.SetMarkedText(text, selectionStart, selectionLength);
        return true;
    }

    /// <summary>Ends the composition, keeping its text.</summary>
    public void Unmark()
    {
        if (Client is { } client && IsComposing)
        {
            IsComposing = false;
            client.UnmarkText();
        }
    }
}
