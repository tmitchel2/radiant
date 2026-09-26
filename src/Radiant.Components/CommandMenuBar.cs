using System;
using System.Collections.Generic;
using System.Linq;
using Radiant.Platform;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// The menu bar made from the UI's registered commands (<c>context.UseCommand</c>): each command
/// with a <see cref="Command.Menu"/> is an item on that menu, in the order it registered, with a
/// divider between groups, its shortcut beside it, greyed while it's disabled and ticked while
/// it's checked. Where the platform has its own menu bar (macOS's), the menus go there and this
/// draws nothing; otherwise it draws a <see cref="MenuBar"/>. Either way it follows the commands as
/// they change.
/// </summary>
public sealed record CommandMenuBar : Component
{
    /// <summary>The order menus come in; others follow, in the order their first command registered.</summary>
    public IReadOnlyList<string> MenuOrder { get; init; } = ["File", "Edit", "View", "Go", "Window", "Help"];

    /// <summary>Whether to use the platform's menu bar where it has one (the default); off always draws it.</summary>
    public bool PreferPlatformMenuBar { get; init; } = true;

    /// <summary>
    /// Whether to draw the menu bar in the window where it isn't the platform's (the default). Off,
    /// an app whose commands are all reachable otherwise (a palette, a toolbar) shows menus only
    /// where the platform has a menu bar.
    /// </summary>
    public bool DrawWithoutPlatformMenuBar { get; init; } = true;

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var commands = context.UseCommands();
        var registry = context.Root.Commands;
        var service = context.UsePlatform().Menus;
        var native = PreferPlatformMenuBar && service.HasMenuBar;
        var menus = Menus(commands, MenuOrder);

        context.UseEffect(() =>
        {
            if (!native)
            {
                return null;
            }
            var ids = menus.Select(m => Items(m.Commands).Select(i => i.Command?.Id).ToArray()).ToArray();
            service.SetMenuBar([.. menus.Select(m => new PlatformMenu(m.Title, [.. Items(m.Commands).Select(PlatformItem)]))], (menu, item) =>
            {
                if (menu < ids.Length && item < ids[menu].Length && ids[menu][item] is { } id)
                {
                    registry.Execute(id);
                }
            });
            return () => service.SetMenuBar([], static (_, _) => { });
        }, (native, commands, menus.Count));

        if (native || !DrawWithoutPlatformMenuBar)
        {
            return null;
        }
        return new MenuBar([.. menus.Select(m => new MenuBarMenu(m.Title, [.. m.Commands.Select((c, i) => new MenuItem(c.Title, () => registry.Execute(c.Id))
        {
            Icon = c.Checked == true ? "check" : c.Icon,
            Shortcut = c.Shortcut?.ToString(),
            Disabled = !c.Enabled,
            DividerBefore = i > 0 && c.Group != m.Commands[i - 1].Group,
        })]))]);
    }

    // The commands on the menu bar, by menu: the ordered menus first, then the rest as they appear.
    private static List<(string Title, IReadOnlyList<Command> Commands)> Menus(IReadOnlyList<Command> commands, IReadOnlyList<string> order)
    {
        var titles = commands.Where(c => c.Menu is not null).Select(c => c.Menu!).Distinct().ToList();
        var ordered = order.Where(titles.Contains).Concat(titles.Where(t => !order.Contains(t)));
        return [.. ordered.Select(t => (t, (IReadOnlyList<Command>)[.. commands.Where(c => c.Menu == t)]))];
    }

    // A menu's entries: its commands, with a separator (no command) between groups.
    private static IEnumerable<(Command? Command, bool Separator)> Items(IReadOnlyList<Command> commands)
    {
        for (var i = 0; i < commands.Count; i++)
        {
            if (i > 0 && commands[i].Group != commands[i - 1].Group)
            {
                yield return (null, true);
            }
            yield return (commands[i], false);
        }
    }

    // A platform menu takes shortcuts only with ⌘ or ⌃ held, or on a function key: it answers to
    // them before the window sees the key, so a bare key (Delete, a letter) would never reach a
    // text field. Those still work as the UI's shortcuts; the menu just doesn't show them.
    private static PlatformMenuItem PlatformItem((Command? Command, bool Separator) entry)
    {
        if (entry.Command is not { } command)
        {
            return PlatformMenuItem.Separator;
        }
        var shortcut = command.Shortcut?.ToMenuShortcut();
        var takesIt = shortcut is not null
            && ((shortcut.Modifiers & (MenuModifiers.Command | MenuModifiers.Control)) != 0 || shortcut.Key.StartsWith('F') && shortcut.Key.Length > 1);
        return new PlatformMenuItem(command.Title)
        {
            Enabled = command.Enabled,
            Checked = command.Checked == true,
            Shortcut = takesIt ? shortcut : null,
        };
    }
}
