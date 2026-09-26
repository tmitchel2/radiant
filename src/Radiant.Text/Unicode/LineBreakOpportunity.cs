namespace Radiant.Text.Unicode;

/// <summary>
/// A place a line may end: before the UTF-16 code unit at <paramref name="Index"/>. Most are
/// opportunities layout may take when a line fills up; mandatory ones (after a line feed, paragraph
/// separator and so on, and at the end of the text) end the line whether it is full or not.
/// </summary>
/// <param name="Index">The UTF-16 index of the first code unit on the next line.</param>
/// <param name="IsMandatory">Whether the line must end here rather than merely may.</param>
public readonly record struct LineBreakOpportunity(int Index, bool IsMandatory);
