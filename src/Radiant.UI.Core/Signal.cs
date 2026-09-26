using System;
using System.Collections.Generic;

namespace Radiant.UI.Core;

/// <summary>
/// A value that notifies its subscribers when it changes: state that lives outside any one
/// component, such as a model or a setting. Components read it with
/// <see cref="BuildContext.Watch{T}"/> and are rebuilt when it changes. Use from the UI thread.
/// </summary>
/// <typeparam name="T">The value's type.</typeparam>
public sealed class Signal<T> : IReadable<T>
{
    private readonly List<Action> _subscribers = [];
    private T _value;

    /// <summary>A signal holding <paramref name="value"/>.</summary>
    public Signal(T value) => _value = value;

    /// <summary>The value. Setting an unequal value notifies every subscriber.</summary>
    public T Value
    {
        get => _value;
        set
        {
            if (EqualityComparer<T>.Default.Equals(_value, value))
            {
                return;
            }
            _value = value;
            foreach (var subscriber in _subscribers.ToArray())
            {
                subscriber();
            }
        }
    }

    /// <inheritdoc/>
    public IDisposable Subscribe(Action onChange)
    {
        ArgumentNullException.ThrowIfNull(onChange);
        _subscribers.Add(onChange);
        return new Subscription(() => _subscribers.Remove(onChange));
    }

    private sealed class Subscription(Action unsubscribe) : IDisposable
    {
        private Action? _unsubscribe = unsubscribe;

        public void Dispose()
        {
            _unsubscribe?.Invoke();
            _unsubscribe = null;
        }
    }
}
