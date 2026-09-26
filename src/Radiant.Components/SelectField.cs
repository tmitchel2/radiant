using System;
using System.Collections.Generic;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A field that picks one of a list of options from a menu dropping from it (as wide as the field),
/// the chosen one ticked. Controlled: shows <see cref="Selected"/> and reports each choice.
/// </summary>
/// <param name="Label">The field's label.</param>
/// <param name="Options">The choices.</param>
/// <param name="Selected">The chosen option's index, or -1 for none.</param>
/// <param name="OnSelect">Called with the chosen index.</param>
public sealed record SelectField(string Label, IReadOnlyList<string> Options, int Selected, Action<int>? OnSelect) : Component
{
    /// <summary>Filled or outlined, as <see cref="TextField"/>.</summary>
    public TextFieldVariant Variant { get; init; }

    /// <summary>Help under the field.</summary>
    public string? SupportingText { get; init; }

    /// <summary>Whether it can't be used.</summary>
    public bool Disabled { get; init; }

    /// <summary>Size and placement.</summary>
    public Radiant.Layout.LayoutStyle? Layout { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var anchor = context.UseRef(new ElementRef()).Value;
        var open = context.UseState(false);
        var text = Selected >= 0 && Selected < Options.Count ? Options[Selected] : "";
        var select = OnSelect;
        var disabled = Disabled;
        var items = new List<MenuItem>();
        for (var i = 0; i < Options.Count; i++)
        {
            var index = i;
            items.Add(new MenuItem(Options[i], () => select?.Invoke(index)) { Icon = i == Selected ? "check" : null });
        }
        return new Fragment(
            new Box
            {
                Ref = anchor,
                Layout = Layout ?? default,
                OnClick = _ =>
                {
                    if (!disabled)
                    {
                        open.Set(true);
                    }
                },
                OnKeyDown = e =>
                {
                    if (!disabled && e.Key is KeyCode.Space or KeyCode.Enter or KeyCode.Down)
                    {
                        open.Set(true);
                        e.Handled = true;
                    }
                },
                Children =
                [
                    new TextField(Label)
                    {
                        Value = TextEditState.From(text),
                        ReadOnly = true,
                        Disabled = Disabled,
                        Variant = Variant,
                        SupportingText = SupportingText,
                        TrailingIcon = open.Value ? "arrow_drop_up" : "arrow_drop_down",
                    },
                ],
            },
            new Menu(anchor, open.Value, () => open.Set(false), items) { MatchAnchorWidth = true });
    }
}
