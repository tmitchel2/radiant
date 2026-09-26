namespace Radiant.Platform;

/// <summary>Accessibility without assistive technology: it keeps the provider, for tests to read the tree through.</summary>
public sealed class HeadlessAccessibility : IAccessibility
{
    /// <inheritdoc/>
    public bool IsSupported => false;

    /// <summary>The provider attached.</summary>
    public IAccessibilityProvider? Provider { get; private set; }

    /// <summary>How many times the tree was said to have changed.</summary>
    public int Invalidations { get; private set; }

    /// <summary>The node focus last moved to.</summary>
    public int FocusedId { get; private set; }

    /// <inheritdoc/>
    public void Attach(IAccessibilityProvider? provider) => Provider = provider;

    /// <inheritdoc/>
    public void Invalidate() => Invalidations++;

    /// <inheritdoc/>
    public void FocusChanged(int id) => FocusedId = id;
}
