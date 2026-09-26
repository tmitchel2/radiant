using System;

namespace Radiant.UI.Core;

/// <summary>A value that can change and says when it does: a <see cref="Signal{T}"/>, or something computed from signals.</summary>
/// <typeparam name="T">The value's type.</typeparam>
public interface IReadable<out T>
{
    /// <summary>The current value.</summary>
    T Value { get; }

    /// <summary>Calls <paramref name="onChange"/> after each change until the result is disposed.</summary>
    IDisposable Subscribe(Action onChange);
}
