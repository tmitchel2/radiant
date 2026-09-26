using System;

namespace Radiant.Platform;

/// <summary>
/// Appearance settings that tests set: light, a blue accent, standard contrast and motion to
/// start with. Changing a setting raises <see cref="Changed"/>, as a user changing it would.
/// </summary>
public sealed class HeadlessAppearance : IAppearance
{
    private bool _isDark;
    private uint _accentColor = 0xFF007AFF;
    private bool _increaseContrast;
    private bool _reduceMotion;

    /// <inheritdoc/>
    public event Action? Changed;

    /// <inheritdoc/>
    public bool IsDark
    {
        get => _isDark;
        set => Change(ref _isDark, value);
    }

    /// <inheritdoc/>
    public uint AccentColor
    {
        get => _accentColor;
        set => Change(ref _accentColor, value);
    }

    /// <inheritdoc/>
    public bool IncreaseContrast
    {
        get => _increaseContrast;
        set => Change(ref _increaseContrast, value);
    }

    /// <inheritdoc/>
    public bool ReduceMotion
    {
        get => _reduceMotion;
        set => Change(ref _reduceMotion, value);
    }

    private void Change<T>(ref T field, T value)
    {
        if (!Equals(field, value))
        {
            field = value;
            Changed?.Invoke();
        }
    }
}
