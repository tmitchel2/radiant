using System;
using System.Collections.Generic;
using Radiant.Components;
using Radiant.UI.Core;

namespace Radiant.Gallery.ThemeLab;

/// <summary>
/// A compact drop-down for a property grid: a button showing the choice, opening a menu of the
/// options. A <see cref="SelectField"/> always shows its label, which the grid already shows beside it.
/// </summary>
/// <param name="Name">What's being chosen, for assistive technology.</param>
/// <param name="Options">The options' names.</param>
/// <param name="Selected">The chosen option.</param>
/// <param name="OnSelect">Called with the option chosen.</param>
internal sealed partial record ChoiceButton(string Name, IReadOnlyList<string> Options, int Selected, Action<int> OnSelect) : Component
{
    [TestId<SurfaceButton>] public static partial string Button { get; }

    public override Element? Build(BuildContext context)
    {
        var anchor = context.UseRef(new ElementRef()).Value;
        var open = context.UseState(false);
        var select = OnSelect;
        var items = new List<MenuItem>();
        for (var i = 0; i < Options.Count; i++)
        {
            var index = i;
            items.Add(new MenuItem(Options[i], () => select(index)) { Icon = i == Selected ? "check" : null });
        }
        var text = Selected >= 0 && Selected < Options.Count ? Options[Selected] : "";
        return new Fragment(
            new Box
            {
                Ref = anchor,
                Children = [new SurfaceButton(text, ButtonVariant.Outlined) { TestId = Button, TrailingIcon = "arrow_drop_down", OnPress = () => open.Set(true) }],
            },
            new Menu(anchor, open.Value, () => open.Set(false), items));
    }
}
