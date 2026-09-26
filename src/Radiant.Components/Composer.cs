using System;
using System.Collections.Generic;
using Radiant.Layout;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A framed box to write a message or a prompt in: an optional header (a mode switch, the file
/// being replied to), the text, which grows line by line, and a row of actions ending in a send
/// button. Send is disabled until there's something to send; ⌘Enter (Ctrl+Enter) sends too.
/// Controlled: shows <paramref name="Value"/> and reports each edit.
/// </summary>
/// <param name="Value">The text.</param>
/// <param name="OnChange">Called after each edit.</param>
[RequiresTestId]
public sealed partial record Composer(TextEditState Value, Action<TextEditState> OnChange) : Component
{
    [TestId<TextField>] public static partial string Field { get; }
    [TestId<SurfaceButton>] public static partial string Send { get; }

    /// <summary>What assistive technology calls the text.</summary>
    public string Label { get; init; } = "Message";

    /// <summary>What it says while empty.</summary>
    public string? Placeholder { get; init; }

    /// <summary>Above the text: a mode switch, a quoted message.</summary>
    public Element? Header { get; init; }

    /// <summary>At the start of the bottom row: attach, a model picker.</summary>
    public IReadOnlyList<Element?> Actions { get; init; } = [];

    /// <summary>The send button's label.</summary>
    public string SendLabel { get; init; } = "Send";

    /// <summary>Called with the text when it's sent.</summary>
    public Action<string>? OnSend { get; init; }

    /// <summary>Size and placement.</summary>
    public LayoutStyle? Layout { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var text = Value.Text;
        var ready = text.Trim().Length > 0;
        var send = OnSend;
        void Submit()
        {
            if (ready)
            {
                send?.Invoke(text);
            }
        }
        return new Card(
            Header,
            new TextField(Label)
            {
                TestId = Field,
                Variant = TextFieldVariant.Plain,
                Multiline = true,
                Placeholder = Placeholder,
                Value = Value,
                OnChange = OnChange,
                OnSubmit = Submit,
                Layout = new LayoutStyle { MinHeight = 44 },
            },
            new Box
            {
                Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 4 },
                Children =
                [
                    .. Actions,
                    new Box { Layout = new LayoutStyle { FlexGrow = 1 } },
                    new SurfaceButton(SendLabel) { TestId = Send, TrailingIcon = "arrow_forward", ShowDisabled = ready ? null : true, OnPress = Submit },
                ],
            })
        {
            Variant = CardVariant.Outlined,
            Layout = new LayoutStyle { Padding = Edges.All(12), RowGap = 8 }.Merge(Layout ?? default),
        };
    }
}
