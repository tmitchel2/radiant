using System;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A decision the user must make before going on (discard changes, delete for good): a
/// <see cref="Dialog"/> that Escape and the scrim don't close, with a confirm button and a cancel
/// button, the confirm one in the error colour when it destroys something.
/// </summary>
/// <param name="Open">Whether it's showing.</param>
/// <param name="Title">The question.</param>
/// <param name="OnConfirm">What confirming does (the dialog closes through <see cref="OnCancel"/>'s owner or this).</param>
/// <param name="OnCancel">What cancelling does.</param>
public sealed partial record AlertDialog(bool Open, string Title, Action OnConfirm, Action OnCancel) : Component
{
    [TestId<SurfaceButton>] public static partial string Confirm { get; }
    [TestId<SurfaceButton>] public static partial string Cancel { get; }

    /// <summary>What happens, in a sentence.</summary>
    public string? Text { get; init; }

    /// <summary>The confirm button's label.</summary>
    public string ConfirmText { get; init; } = "Confirm";

    /// <summary>The cancel button's label.</summary>
    public string CancelText { get; init; } = "Cancel";

    /// <summary>Whether confirming destroys something: the button is then in the error colour.</summary>
    public bool Destructive { get; init; }

    /// <summary>An icon above the title.</summary>
    public string? Icon { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context) => new Dialog(Open, OnCancel)
    {
        Icon = Icon,
        Title = Title,
        Text = Text,
        Dismissible = false,
        Actions =
        [
            new SurfaceButton(CancelText, ButtonVariant.Text) { TestId = Cancel, OnPress = OnCancel },
            Destructive
                ? new SurfaceButton(ConfirmText) { TestId = Confirm, OnPress = OnConfirm, SurfaceColor = Radiant.Theming.SurfaceName.Error }
                : new SurfaceButton(ConfirmText) { TestId = Confirm, OnPress = OnConfirm },
        ],
    };
}
