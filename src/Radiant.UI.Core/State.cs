using System;
using System.Collections.Generic;

namespace Radiant.UI.Core;

/// <summary>
/// A component's piece of state, from <see cref="BuildContext.UseState{T}(T)"/>. Setting an unequal
/// value rebuilds the component (on the next frame, once, however many times it's set).
/// </summary>
/// <typeparam name="T">The value's type.</typeparam>
public sealed class State<T>
{
    private readonly ElementNode _owner;

    internal State(T value, ElementNode owner)
    {
        Value = value;
        _owner = owner;
    }

    /// <summary>The value as of now: after a <see cref="Set"/>, the new value, before any rebuild.</summary>
    public T Value { get; private set; }

    /// <summary>Changes the value, rebuilding the component if it's unequal to the current one.</summary>
    public void Set(T value)
    {
        if (EqualityComparer<T>.Default.Equals(Value, value))
        {
            return;
        }
        Value = value;
        _owner.Root.MarkDirty(_owner);
    }

    /// <summary>Changes the value based on the current one: <c>count.Update(n => n + 1)</c>.</summary>
    public void Update(Func<T, T> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        Set(update(Value));
    }
}
