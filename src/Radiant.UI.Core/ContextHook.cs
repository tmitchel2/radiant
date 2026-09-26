namespace Radiant.UI.Core;

/// <summary>The provider a component reads a context from, so it can stop listening when it unmounts.</summary>
internal sealed class ContextHook(ElementNode consumer) : IHook
{
    public ElementNode? Provider { get; set; }

    public void Release()
    {
        Provider?.Consumers?.Remove(consumer);
        Provider = null;
    }
}
