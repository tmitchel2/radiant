namespace Radiant.Platform;

/// <summary>
/// The platform's text input: key presses turned into text by the user's keyboard layout and
/// input method, delivered to the focused <see cref="ITextInputClient"/>.
/// <para>
/// While a client has focus, typed text goes to it and not to the UI's text events. Key presses
/// still reach the UI, except while an input method is composing: then the platform holds them
/// back (on macOS, all of them), as the input method is using them (Enter to accept a
/// conversion, arrows to pick a candidate) and they mustn't be acted on twice.
/// </para>
/// </summary>
public interface ITextInput
{
    /// <summary>The client typing goes to, or null.</summary>
    ITextInputClient? Client { get; }

    /// <summary>Whether an input method is part way through composing text for the client.</summary>
    bool IsComposing { get; }

    /// <summary>
    /// Sends typing to <paramref name="client"/>, or back to the UI's text events if null. A
    /// composition in progress for the previous client ends: it keeps its text
    /// (<see cref="ITextInputClient.UnmarkText"/>) and the input method is reset.
    /// </summary>
    void Focus(ITextInputClient? client);

    /// <summary>
    /// Tells the platform the client's <see cref="ITextInputClient.CaretRect"/> changed, so an
    /// open candidate window moves with it.
    /// </summary>
    void InvalidateCaret();
}
