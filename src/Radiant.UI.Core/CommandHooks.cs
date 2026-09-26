using System;
using System.Collections.Generic;

namespace Radiant.UI.Core;

/// <summary>The command hooks.</summary>
public static class CommandHooks
{
    /// <summary>
    /// Registers <paramref name="command"/> while this component is mounted: its shortcut works,
    /// and palettes and the menu bar list it. It's kept up to date with each build, so it can say
    /// whether it's enabled or checked now; what it runs is always this build's.
    /// </summary>
    public static void UseCommand(this BuildContext context, Command command)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);
        var latest = context.UseRef(command);
        latest.Value = command;
        var registration = context.UseRef<CommandRegistration?>(null);
        var (root, node) = (context.Root, context.Node);
        var depth = node.Depth;
        // Registered after the first build and kept up to date after each: the frame's effects run
        // before it's drawn, so a menu bar or palette lists it in the same frame.
        context.UseEffect(() =>
        {
            registration.Value = root.Commands.Add(Live(latest.Value), depth, node);
            return () =>
            {
                registration.Value?.Dispose();
                registration.Value = null;
            };
        }, default(ValueTuple));
        context.UseEffect(() =>
        {
            registration.Value?.Update(Live(latest.Value));
            return null;
        });

        // Its Run reads the latest build's, so the registry needn't hear of every new closure.
        Command Live(Command c) => c with { Run = c.Run is null ? null : () => latest.Value.Run?.Invoke() };
    }

    /// <summary>The commands registered in this UI, rebuilding this component when they change.</summary>
    public static IReadOnlyList<Command> UseCommands(this BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var registry = context.Root.Commands;
        var version = context.UseState(0);
        var shown = context.UseRef(registry.Commands);
        shown.Value = registry.Commands;
        context.UseEffect(() =>
        {
            void OnChanged() => version.Set(version.Value + 1);
            registry.Changed += OnChanged;
            // Commands registered between this build and now (by effects that ran first) were missed.
            if (!ReferenceEquals(registry.Commands, shown.Value))
            {
                OnChanged();
            }
            return () => registry.Changed -= OnChanged;
        }, registry);
        return shown.Value;
    }
}
