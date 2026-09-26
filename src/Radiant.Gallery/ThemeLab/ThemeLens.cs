using System;
using Radiant.Theming;

namespace Radiant.Gallery.ThemeLab;

/// <summary>
/// One value inside a theme, read and written through the records that hold it: how the editor
/// reaches <c>theme.Components.Button.Filled.Surface</c> without a hand-written <c>with</c> chain for
/// every property.
/// </summary>
/// <param name="read">Reads the value from a theme.</param>
/// <param name="write">A copy of a theme with the value changed.</param>
internal sealed class ThemeLens<T>(Func<Theme, T> read, Func<Theme, T, Theme> write)
{
    public T Read(Theme theme) => read(theme);

    public Theme Write(Theme theme, T value) => write(theme, value);

    /// <summary>A value inside this one.</summary>
    public ThemeLens<TPart> Then<TPart>(Func<T, TPart> get, Func<T, TPart, T> set) =>
        new(theme => get(read(theme)), (theme, part) => write(theme, set(read(theme), part)));

    /// <summary>This value, converted (an int or a duration edited as a float).</summary>
    public ThemeLens<TOther> Map<TOther>(Func<T, TOther> to, Func<TOther, T> from) =>
        new(theme => to(read(theme)), (theme, other) => write(theme, from(other)));
}
