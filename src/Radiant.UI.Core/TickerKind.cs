namespace Radiant.UI.Core;

/// <summary>
/// What a ticker (<see cref="UIRoot.AddTicker(System.Action{double}, TickerKind, string?)"/>) is doing, which
/// decides whether the UI counts as busy while it runs (<see cref="UIRoot.IsIdle"/>). Every kind gets frames.
/// </summary>
public enum TickerKind
{
    /// <summary>Something moving to an end: a transition, a settle. The UI isn't idle until it stops.</summary>
    Animation,

    /// <summary>Something that moves for as long as it's shown: a spinner, a caret's blink. Never keeps the UI busy.</summary>
    Continuous,

    /// <summary>A wait before something happens: a tooltip's delay, a snackbar's timeout. Doesn't keep the UI busy.</summary>
    Timer,
}
