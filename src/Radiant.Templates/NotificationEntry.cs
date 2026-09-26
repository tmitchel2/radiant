namespace Radiant.Templates;

/// <summary>A notification, for <see cref="NotificationFeed"/>.</summary>
/// <param name="Icon">What kind it is, as an icon.</param>
/// <param name="Text">What happened ("Ada commented on your design").</param>
/// <param name="Time">When, as shown ("5 min ago").</param>
public sealed record NotificationEntry(string Icon, string Text, string Time)
{
    /// <summary>Whether it hasn't been seen yet.</summary>
    public bool Unread { get; init; }
}
