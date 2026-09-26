using System;
using Radiant.Text;

namespace Radiant.UI.Core;

/// <summary>
/// A selection in editable text: where it was started (<see cref="Anchor"/>) and where it's been
/// dragged or extended to (<see cref="Focus"/>, where the caret is drawn). Collapsed, it is a caret.
/// </summary>
/// <param name="Anchor">The fixed end.</param>
/// <param name="Focus">The moving end, where the caret is.</param>
/// <param name="Affinity">Which side of a wrap or direction change the caret belongs to.</param>
public readonly record struct TextSelection(int Anchor, int Focus, TextAffinity Affinity = TextAffinity.Downstream)
{
    /// <summary>A caret at <paramref name="index"/>.</summary>
    public static TextSelection Caret(int index, TextAffinity affinity = TextAffinity.Downstream) => new(index, index, affinity);

    /// <summary>The first selected index.</summary>
    public int Start => Math.Min(Anchor, Focus);

    /// <summary>One past the last selected index.</summary>
    public int End => Math.Max(Anchor, Focus);

    /// <summary>Whether nothing is selected (just a caret).</summary>
    public bool IsCollapsed => Anchor == Focus;

    /// <summary>The caret's position.</summary>
    public TextPosition FocusPosition => new(Focus, Affinity);

    /// <summary>The selection as a range.</summary>
    public TextRange Range => new(Start, End);
}
