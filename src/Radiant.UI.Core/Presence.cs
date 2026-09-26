using System;
using Radiant.Animation;

namespace Radiant.UI.Core;

/// <summary>
/// Shows content that animates in and out: when <see cref="Visible"/> turns false the content stays
/// mounted while its progress runs down to 0, then goes. The content is built from the progress
/// (0 hidden, 1 shown), so it can fade, scale or slide as it likes.
/// <code>
/// new Presence(open, progress => new Box { Opacity = progress, Children = [dialog] })
/// </code>
/// </summary>
/// <param name="Visible">Whether the content should be shown.</param>
/// <param name="Content">Builds the content at a progress from 0 to 1.</param>
public sealed record Presence(bool Visible, Func<float, Element?> Content) : Component
{
    /// <summary>How long entering and leaving take.</summary>
    public TimeSpan Duration { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>The curve for entering.</summary>
    public Easing EnterEasing { get; init; } = Easing.EmphasizedDecelerate;

    /// <summary>The curve for leaving.</summary>
    public Easing ExitEasing { get; init; } = Easing.EmphasizedAccelerate;

    /// <summary>Whether content visible when first mounted animates in (true) or starts shown.</summary>
    public bool AnimateOnMount { get; init; } = true;

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var progress = context.UseTransition(Visible ? 1f : 0f, Duration, Visible ? EnterEasing : ExitEasing,
            initial: AnimateOnMount ? 0f : null);
        return !Visible && progress <= 0f ? null : Content(progress);
    }
}
