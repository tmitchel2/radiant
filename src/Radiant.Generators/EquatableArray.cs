using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Radiant.Generators;

/// <summary>
/// An immutable array compared by its items, so the generator's models compare by value and
/// incremental generation can tell when nothing changed.
/// </summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> _items;

    public EquatableArray(ImmutableArray<T> items) => _items = items;

    public EquatableArray(IEnumerable<T> items) => _items = items.ToImmutableArray();

    public int Count => _items.IsDefault ? 0 : _items.Length;

    public bool Equals(EquatableArray<T> other) => AsEnumerable().SequenceEqual(other.AsEnumerable());

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var item in AsEnumerable())
        {
            hash = hash * 31 + item.GetHashCode();
        }
        return hash;
    }

    public IEnumerator<T> GetEnumerator() => AsEnumerable().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private IEnumerable<T> AsEnumerable() => _items.IsDefault ? Enumerable.Empty<T>() : _items;
}
