using System.Collections.Generic;

namespace Radiant.Platform;

/// <summary>A menu on the platform's menu bar (<see cref="IMenuService.SetMenuBar"/>).</summary>
/// <param name="Title">What the bar says ("File").</param>
/// <param name="Items">What the menu offers.</param>
public sealed record PlatformMenu(string Title, IReadOnlyList<PlatformMenuItem> Items);
