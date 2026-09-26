using System;
using System.Linq;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A circle standing for a person or thing: their initials on a container colour chosen from
/// their name (so the same name always gets the same colour), or a person icon without a name.
/// </summary>
/// <param name="Name">The name to take initials and a colour from.</param>
public sealed record Avatar(string? Name) : Component
{
    /// <summary>The diameter.</summary>
    public float Size { get; init; } = 40f;

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var initials = Name is null ? null : string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpperInvariant(w[0])));
        SurfaceName[] families = [SurfaceName.Primary, SurfaceName.Secondary, SurfaceName.Tertiary];
        var family = Name is null ? SurfaceName.Secondary : families[(int)((uint)StableHash(Name) % (uint)families.Length)];
        return new Surface
        {
            SurfaceColor = family,
            SurfaceContainerToggle = true,
            CornerShape = CornerShapeRole.Full,
            Semantics = new Semantics { Role = SemanticsRole.Image, Label = Name },
            Layout = new LayoutStyle { Width = Size, Height = Size, AlignItems = Align.Center, JustifyContent = Justify.Center },
            Children =
            [
                string.IsNullOrEmpty(initials)
                    ? new SurfaceIcon("person") { IconSize = Size * 0.6f }
                    : new SurfaceText(initials) { TextType = Size >= 48 ? TextType.TitleLarge : TextType.TitleMedium },
            ],
        };
    }

    // string.GetHashCode is randomised per process; colours must stay put between runs.
    private static int StableHash(string text)
    {
        var hash = 17;
        foreach (var c in text)
        {
            hash = unchecked(hash * 31 + c);
        }
        return hash;
    }
}
