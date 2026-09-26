using System.Collections.Generic;

namespace Radiant.Components;

/// <summary>A menu on a <see cref="MenuBar"/>: its title and its items.</summary>
/// <param name="Title">What the bar says ("File").</param>
/// <param name="Items">What the menu offers.</param>
public sealed record MenuBarMenu(string Title, IReadOnlyList<MenuItem> Items);
