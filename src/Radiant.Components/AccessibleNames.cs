using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>Names for controls whose name is shown beside them rather than on them.</summary>
public static class AccessibleNames
{
    /// <summary>
    /// <paramref name="control"/> named <paramref name="name"/> for assistive technology, if it has no
    /// name of its own: what a row that shows a label beside its control does (a settings row, a property
    /// in an inspector), so a switch there isn't heard as just "switch".
    /// </summary>
    public static Element? Name(Element? control, string name) => control switch
    {
        Switch { Label: null, AccessibleLabel: null } s => s with { AccessibleLabel = name },
        Checkbox { Label: null, AccessibleLabel: null } c => c with { AccessibleLabel = name },
        Radio { Label: null, AccessibleLabel: null } r => r with { AccessibleLabel = name },
        Slider { Label: null } s => s with { Label = name },
        RangeSlider { Label: null } s => s with { Label = name },
        _ => control,
    };
}
