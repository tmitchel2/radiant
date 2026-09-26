using System;
using System.Collections.Generic;
using System.Linq;
using Radiant.Layout;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A set of radio buttons, one of them chosen, as a platform's radio group behaves: the arrow keys
/// move the choice (and focus) through them, wrapping at the ends, and only the chosen one is a Tab
/// stop, so Tab moves past the group rather than through every option.
/// <code>new RadioGroup(["Small", "Medium", "Large"], size.Value, size.Set) { TestId = Size, Label = "Size" }</code>
/// </summary>
/// <param name="Options">The options' labels.</param>
/// <param name="Selected">The chosen option's index, or -1 for none.</param>
/// <param name="OnSelect">Called with the index of an option chosen.</param>
[RequiresTestId]
public sealed partial record RadioGroup(IReadOnlyList<string> Options, int Selected, Action<int>? OnSelect) : Component
{
    [TestId<Radio>] public static partial string Option { get; }

    /// <summary>The group's name, for assistive technology.</summary>
    public string? Label { get; init; }

    /// <summary>Whether none of it can be used.</summary>
    public bool Disabled { get; init; }

    /// <summary>Whether the options run across rather than down.</summary>
    public bool Horizontal { get; init; }

    /// <summary>Size and placement.</summary>
    public LayoutStyle? Layout { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var count = Options.Count;
        var refs = context.UseMemo(() => Enumerable.Range(0, count).Select(_ => new ElementRef()).ToArray(), count);
        var rightToLeft = context.UseRightToLeft();
        var select = OnSelect;
        // The Tab stop: the chosen option, or the first when none is.
        var stop = Selected >= 0 && Selected < count ? Selected : 0;
        var options = new List<Element?>();
        for (var i = 0; i < count; i++)
        {
            var index = i;
            options.Add(new Box
            {
                Ref = refs[i],
                OnKeyDown = e =>
                {
                    var step = e.Key.ForDirection(rightToLeft) switch
                    {
                        KeyCode.Down or KeyCode.Right => 1,
                        KeyCode.Up or KeyCode.Left => -1,
                        _ => 0,
                    };
                    if (step != 0 && count > 1)
                    {
                        var next = (index + step + count) % count;
                        select?.Invoke(next);
                        refs[next].Focus();
                        e.Handled = true;
                    }
                },
                Children =
                [
                    new Radio(index == Selected, () => select?.Invoke(index))
                    {
                        TestId = Option,
                        Label = Options[index],
                        Disabled = Disabled,
                        TabIndex = index == stop ? 0 : -1,
                    },
                ],
            });
        }
        return new Box
        {
            Semantics = new Semantics { Role = SemanticsRole.Group, Label = Label },
            Layout = new LayoutStyle { FlexDirection = Horizontal ? FlexDirection.Row : FlexDirection.Column, RowGap = 4, ColumnGap = 12 }.Merge(Layout ?? default),
            Children = options,
        };
    }
}
