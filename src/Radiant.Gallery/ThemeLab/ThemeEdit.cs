using System;
using Radiant.Theming;

namespace Radiant.Gallery.ThemeLab;

/// <summary>
/// Changes the theme: <paramref name="change"/> is applied to the theme in force when the edit runs,
/// not one captured when the editor was built, so quick edits in a row all land.
/// </summary>
/// <param name="change">What to do to the theme.</param>
/// <param name="animate">Whether to animate to the result: yes for a choice, no while dragging.</param>
internal delegate void ThemeEdit(Func<Theme, Theme> change, bool animate);
