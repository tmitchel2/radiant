using System;
using System.Collections.Generic;
using System.Linq;

namespace Radiant.UI.Core.Tests;

/// <summary>A component that logs its builds and keeps a counter and a mount id in state.</summary>
internal sealed record Counter(string Name, Log Log) : Component
{
    private static int s_nextId;

    public override Element? Build(BuildContext context)
    {
        Log.Add($"build {Name}");
        var id = context.UseRef(++s_nextId);
        var count = context.UseState(0);
        Handles[Name] = (count, id.Value);
        return new Box { Key = Key };
    }

    public static Dictionary<string, (State<int> Count, int Id)> Handles { get; } = [];

    // Log is a test helper, not a prop that should affect equality.
    public bool Equals(Counter? other) => other is not null && Name == other.Name && Key == other.Key;

    public override int GetHashCode() => HashCode.Combine(Name, Key);
}

/// <summary>A list of counters, keyed by name.</summary>
internal sealed record CounterList(IReadOnlyList<string> Names, Log Log, bool Keyed = true) : Component
{
    public override Element? Build(BuildContext context) =>
        new Box { Children = Names.Select(n => (Element?)new Counter(n, Log) { Key = Keyed ? (Key?)n : null }).ToArray() };
}

/// <summary>A component whose build is a function, for one-off test trees.</summary>
internal sealed record Lambda(Func<BuildContext, Element?> Body, string Name = "") : Component
{
    public override Element? Build(BuildContext context) => Body(context);

    public bool Equals(Lambda? other) => other is not null && ReferenceEquals(Body, other.Body) && Name == other.Name;

    public override int GetHashCode() => HashCode.Combine(Body, Name);
}
