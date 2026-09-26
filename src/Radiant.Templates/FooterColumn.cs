using System;
using System.Collections.Generic;

namespace Radiant.Templates;

/// <summary>A titled column of links in a <see cref="SiteFooter"/>.</summary>
/// <param name="Title">The column's title ("Product", "Company").</param>
/// <param name="Links">Its links, in order.</param>
public sealed record FooterColumn(string Title, IReadOnlyList<string> Links)
{
    /// <summary>Called with a link's text when it's followed.</summary>
    public Action<string>? OnFollow { get; init; }
}
