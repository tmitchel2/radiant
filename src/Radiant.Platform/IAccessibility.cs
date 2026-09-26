namespace Radiant.Platform;

/// <summary>
/// Assistive technology (VoiceOver): the platform reads the UI's accessibility tree from the
/// <see cref="IAccessibilityProvider"/> when assistive technology asks, and passes on its presses and
/// focus moves. The UI says when the tree may have changed and where focus went.
/// </summary>
public interface IAccessibility
{
    /// <summary>Whether assistive technology can read the UI on this platform.</summary>
    bool IsSupported { get; }

    /// <summary>Where the tree comes from; null to stop.</summary>
    void Attach(IAccessibilityProvider? provider);

    /// <summary>The tree may have changed (it's read again when next asked for).</summary>
    void Invalidate();

    /// <summary>Keyboard focus moved to the node <paramref name="id"/> (0 for none).</summary>
    void FocusChanged(int id);
}
