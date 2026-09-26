namespace Radiant.Components;

/// <summary>The interaction state a selection control's indicator is drawn in.</summary>
internal readonly record struct SelectionVisualState(bool Hovered, bool Pressed, bool Focused, bool Disabled);
