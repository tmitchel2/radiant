using System;
using System.Collections.Generic;
using System.Linq;

namespace Radiant.UI.Core;

/// <summary>
/// The commands registered in a UI (<see cref="UIRoot.Commands"/>): each answers to its shortcut
/// while it's the one in force for its id and enabled, and <see cref="Changed"/> tells palettes and
/// menu bars to list them again. Components register theirs with <see cref="CommandHooks.UseCommand"/>.
/// </summary>
/// <remarks>
/// Where several share an id, the one in force is the deepest in the tree of those that apply: a
/// command applies unless it's <see cref="Command.FocusScoped"/> and focus is outside the component
/// that registered it. At the same depth, the later registration wins.
/// </remarks>
public sealed class CommandRegistry
{
    private readonly UIRoot _root;
    private readonly List<Registration> _registrations = [];
    private IReadOnlyList<Command>? _commands;
    private long _sequence;

    internal CommandRegistry(UIRoot root)
    {
        _root = root;
        // Focus moving changes which focus-scoped commands apply.
        root.FocusChanged += () =>
        {
            if (_registrations.Exists(r => r.Command.FocusScoped))
            {
                Invalidate();
            }
        };
    }

    /// <summary>Raised when the commands listed change (other than what they run).</summary>
    public event Action? Changed;

    /// <summary>
    /// The commands in force, one per id, in the order their components come in the tree (a
    /// parent's before its children's, siblings in order), and each component's in the order it
    /// registered them; commands added directly come after. An id whose commands are all
    /// focus-scoped away from focus isn't listed.
    /// </summary>
    public IReadOnlyList<Command> Commands => _commands ??= List();

    /// <summary>The command in force for <paramref name="id"/>, or null.</summary>
    public Command? Find(string id) => Winner(id)?.Command;

    /// <summary>Runs the command in force for <paramref name="id"/> if it's enabled; whether it ran.</summary>
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
    /// registered at <paramref name="depth"/> would. Dispose the result to remove it. A command added
    /// this way belongs to no component, so a focus-scoped one never applies.
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

    private bool Applies(Registration registration) =>
        !registration.Command.FocusScoped || registration.Owner is { } owner && _root.FocusWithin(owner);

    private Registration? Winner(string id)
    {
        Registration? found = null;
        foreach (var registration in _registrations)
        {
            if (registration.Command.Id == id && Applies(registration) && (found is null || registration.Depth >= found.Depth))
            {
                found = registration;
            }
        }
        return found;
    }

    private List<Command> List()
    {
        var ordered = _registrations.Select(r => (Registration: r, Path: r.Path())).ToList();
        ordered.Sort((a, b) =>
        {
            var byPath = ComparePaths(a.Path, b.Path);
            return byPath != 0 ? byPath : a.Registration.Sequence.CompareTo(b.Registration.Sequence);
        });
        // Each id is listed where its first registration comes, as the one in force.
        var list = new List<Command>();
        var seen = new HashSet<string>();
        foreach (var (registration, _) in ordered)
        {
            if (seen.Add(registration.Command.Id) && Winner(registration.Command.Id) is { } winner)
            {
                list.Add(winner.Command);
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

    // Lists again, and tells listeners only if what's listed changed: a focused field's commands
    // update with every keystroke, and a menu bar needn't be rebuilt for each.
    private void Invalidate()
    {
        var old = _commands;
        _commands = null;
        if (old is null || !Same(old, Commands))
        {
            Changed?.Invoke();
        }

        static bool Same(IReadOnlyList<Command> a, IReadOnlyList<Command> b) =>
            a.Count == b.Count && a.Zip(b).All(p => p.First with { Run = null } == p.Second with { Run = null });
    }

    private sealed class Registration(CommandRegistry registry, Command command, int depth, ElementNode? owner, long sequence) : CommandRegistration
    {
        private IDisposable? _shortcut;

        public Command Command { get; private set; } = command;

        public int Depth => depth;

        public long Sequence => sequence;

        public ElementNode? Owner => owner;

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
                if (old.Shortcut != command.Shortcut)
                {
                    _shortcut?.Dispose();
                    Bind();
                }
                registry.Invalidate();
            }
        }

        // The shortcut runs the command only while it's the one in force and enabled; otherwise the
        // key goes on to whatever else answers to it.
        public void Bind() => _shortcut = Command.Shortcut is { } chord
            ? registry._root.AddShortcut(chord, () =>
            {
                if (!ReferenceEquals(registry.Winner(Command.Id), this) || !Command.Enabled)
                {
                    return false;
                }
                Command.Run?.Invoke();
                return true;
            }, depth)
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
