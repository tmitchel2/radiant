using System;
using System.Collections.Generic;
using System.Linq;

namespace Radiant.UI.Core;

/// <summary>
/// The commands registered in a UI (<see cref="UIRoot.Commands"/>): each answers to its shortcut
/// while it's enabled, and <see cref="Changed"/> tells palettes and menu bars to list them again.
/// Components register theirs with <see cref="CommandHooks.UseCommand"/>.
/// </summary>
public sealed class CommandRegistry
{
    private readonly UIRoot _root;
    private readonly List<Registration> _registrations = [];
    private IReadOnlyList<Command>? _commands;
    private long _sequence;

    internal CommandRegistry(UIRoot root) => _root = root;

    /// <summary>Raised when a command is added, removed or changed (other than what it runs).</summary>
    public event Action? Changed;

    /// <summary>
    /// The commands, in the order their components come in the tree (a parent's before its
    /// children's, siblings in order), and each component's in the order it registered them;
    /// commands added directly come after. Where two share an id, the one registered deeper in the
    /// tree (an editor's Copy over the app's) is listed, in the first one's place; at the same
    /// depth, the later.
    /// </summary>
    public IReadOnlyList<Command> Commands => _commands ??= List();

    /// <summary>The command with <paramref name="id"/>, or null.</summary>
    public Command? Find(string id)
    {
        Registration? found = null;
        foreach (var registration in _registrations)
        {
            if (registration.Command.Id == id && (found is null || registration.Depth >= found.Depth))
            {
                found = registration;
            }
        }
        return found?.Command;
    }

    /// <summary>Runs the command with <paramref name="id"/> if there is one and it's enabled; whether it ran.</summary>
    public bool Execute(string id)
    {
        if (Find(id) is not { Enabled: true, Run: { } run })
        {
            return false;
        }
        run();
        return true;
    }

    /// <summary>
    /// Registers <paramref name="command"/>, with its shortcut taking precedence as a shortcut
    /// registered at <paramref name="depth"/> would. Dispose the result to remove it.
    /// </summary>
    public CommandRegistration Add(Command command, int depth = 0) => Add(command, depth, null);

    internal CommandRegistration Add(Command command, int depth, ElementNode? owner)
    {
        ArgumentNullException.ThrowIfNull(command);
        var registration = new Registration(this, command, depth, owner, _sequence++);
        _registrations.Add(registration);
        registration.Bind();
        Invalidate();
        return registration;
    }

    private List<Command> List()
    {
        var list = new List<Command>();
        var seen = new Dictionary<string, (int At, int Depth)>();
        var ordered = _registrations.Select(r => (Registration: r, Path: r.Path())).ToList();
        ordered.Sort((a, b) =>
        {
            var byPath = ComparePaths(a.Path, b.Path);
            return byPath != 0 ? byPath : a.Registration.Sequence.CompareTo(b.Registration.Sequence);
        });
        foreach (var (registration, _) in ordered)
        {
            var command = registration.Command;
            if (seen.TryGetValue(command.Id, out var shown))
            {
                if (registration.Depth >= shown.Depth)
                {
                    list[shown.At] = command;
                    seen[command.Id] = (shown.At, registration.Depth);
                }
            }
            else
            {
                seen[command.Id] = (list.Count, registration.Depth);
                list.Add(command);
            }
        }
        return list;
    }

    // Tree order: by slot from the root down; a parent (a prefix) before its children; no path last.
    private static int ComparePaths(int[]? a, int[]? b)
    {
        if (a is null || b is null)
        {
            return a is null ? (b is null ? 0 : 1) : -1;
        }
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            if (a[i] != b[i])
            {
                return a[i].CompareTo(b[i]);
            }
        }
        return a.Length.CompareTo(b.Length);
    }

    private void Invalidate()
    {
        _commands = null;
        Changed?.Invoke();
    }

    private sealed class Registration(CommandRegistry registry, Command command, int depth, ElementNode? owner, long sequence) : CommandRegistration
    {
        private IDisposable? _shortcut;

        public Command Command { get; private set; } = command;

        public int Depth => depth;

        public long Sequence => sequence;

        // Where the owner is in the tree, as slots from the root down.
        public int[]? Path()
        {
            if (owner is null)
            {
                return null;
            }
            var path = new List<int>();
            for (var node = owner; node is not null; node = node.Parent)
            {
                path.Add(node.Slot);
            }
            path.Reverse();
            return [.. path];
        }

        public override void Update(Command command)
        {
            ArgumentNullException.ThrowIfNull(command);
            var old = Command;
            Command = command;
            // What it runs changes with every build; only a change to what's shown is news.
            if (old with { Run = null } != command with { Run = null })
            {
                _shortcut?.Dispose();
                Bind();
                registry.Invalidate();
            }
        }

        public void Bind() => _shortcut = Command is { Enabled: true, Shortcut: { } chord }
            ? registry._root.AddShortcut(chord, () => Command.Run?.Invoke(), depth)
            : null;

        public override void Dispose()
        {
            if (registry._registrations.Remove(this))
            {
                _shortcut?.Dispose();
                registry.Invalidate();
            }
        }
    }
}
