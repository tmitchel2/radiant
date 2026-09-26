namespace Radiant.Platform;

/// <summary>Where the accessibility tree comes from, and what acting on its nodes does: the UI.</summary>
public interface IAccessibilityProvider
{
    /// <summary>The tree as it is now.</summary>
    AccessibilityNode Root();

    /// <summary>Presses a node, as the user pressing it would. False if there's no such node.</summary>
    bool Press(int id);

    /// <summary>Moves keyboard focus to a node. False if it can't take it.</summary>
    bool Focus(int id);

    /// <summary>The node with keyboard focus, or 0.</summary>
    int FocusedId { get; }
}
