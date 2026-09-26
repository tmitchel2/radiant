using System;

namespace Radiant.Testing;

/// <summary>An image didn't match its golden.</summary>
public sealed class GoldenMismatchException : Exception
{
    /// <summary>A mismatch described by <paramref name="message"/>.</summary>
    public GoldenMismatchException(string message)
        : base(message)
    {
    }

    /// <summary>A mismatch.</summary>
    public GoldenMismatchException()
    {
    }

    /// <summary>A mismatch caused by <paramref name="inner"/>.</summary>
    public GoldenMismatchException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
