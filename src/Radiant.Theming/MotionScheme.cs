using System;
using Radiant.Animation;

namespace Radiant.Theming;

/// <summary>How long and with what curve things move: Material 3's motion tokens.</summary>
public sealed record MotionScheme
{
    /// <summary>Small, quick changes: selection, icons (Material's short durations: 50–200 ms).</summary>
    public TimeSpan ShortDuration { get; init; } = TimeSpan.FromMilliseconds(150);

    /// <summary>Components expanding or moving (medium: 250–400 ms).</summary>
    public TimeSpan MediumDuration { get; init; } = TimeSpan.FromMilliseconds(300);

    /// <summary>Large areas changing (long: 450–600 ms).</summary>
    public TimeSpan LongDuration { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>The curve for most transitions.</summary>
    public Easing Standard { get; init; } = Easing.Standard;

    /// <summary>The curve for things arriving.</summary>
    public Easing Enter { get; init; } = Easing.EmphasizedDecelerate;

    /// <summary>The curve for things leaving.</summary>
    public Easing Exit { get; init; } = Easing.EmphasizedAccelerate;

    /// <summary>Whether to skip animation (the user asked for reduced motion): changes happen at once.</summary>
    public bool Reduced { get; init; }
}
