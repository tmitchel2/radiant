using System.Collections.Generic;

namespace Radiant.Components;

/// <summary>An item of a <see cref="TreeView"/>, with its children.</summary>
/// <param name="Id">What identifies it (unique in the tree): selection and expansion are kept by id.</param>
/// <param name="Label">What it says.</param>
public sealed record TreeNode(string Id, string Label)
{
    /// <summary>An icon before the label.</summary>
    public string? Icon { get; init; }

    /// <summary>The icon while expanded (an open folder); <see cref="Icon"/> if null.</summary>
    public string? ExpandedIcon { get; init; }

    /// <summary>A short text at the row's end (a count, a status).</summary>
    public string? Trailing { get; init; }

    /// <summary>Its children; it can expand if there are any.</summary>
    public IReadOnlyList<TreeNode> Children { get; init; } = [];
}
