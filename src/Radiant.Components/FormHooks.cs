using System;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>The form hook.</summary>
public static class FormHooks
{
    /// <summary>
    /// A form kept for this component's life: its fields' texts, touched states and checks. The
    /// component is rebuilt whenever a field changes.
    /// </summary>
    public static Form UseForm(this BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var version = context.UseState(0);
        var form = context.UseRef<Form?>(null);
        return form.Value ??= new Form(() => version.Update(v => v + 1));
    }
}
