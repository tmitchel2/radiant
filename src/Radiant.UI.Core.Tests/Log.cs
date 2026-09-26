using System.Collections.Generic;

namespace Radiant.UI.Core.Tests;

/// <summary>What test components did, in order.</summary>
internal sealed class Log
{
    public List<string> Entries { get; } = [];

    public void Add(string entry) => Entries.Add(entry);

    public int Count(string entry) => Entries.FindAll(e => e == entry).Count;

    public void Clear() => Entries.Clear();
}
