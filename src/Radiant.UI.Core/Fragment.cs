using System.Collections.Generic;

namespace Radiant.UI.Core;

/// <summary>Several elements where one is expected, without a box around them.</summary>
public sealed record Fragment : Element
{
    /// <summary>A fragment of the given elements (null entries hold a place and show nothing).</summary>
    public Fragment(params Element?[] children) => Children = children;

    /// <summary>The elements, in order.</summary>
    public IReadOnlyList<Element?> Children { get; init; }
}
