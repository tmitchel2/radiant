using System;

namespace Radiant.UI.Core;

/// <summary>A subscription to a signal, renewed if the component starts watching a different one.</summary>
internal sealed class SubscriptionHook : IHook
{
    public object? Source { get; set; }

    public IDisposable? Subscription { get; set; }

    public void Release()
    {
        Subscription?.Dispose();
        Subscription = null;
        Source = null;
    }
}
