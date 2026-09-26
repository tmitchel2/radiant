using System;
using System.Collections.Generic;
using System.Numerics;
using Radiant.Layout;
using Radiant.Text;

namespace Radiant.UI.Core;

/// <summary>
/// Editable text, controlled by its owner: it shows <see cref="State"/> and reports every edit
/// through <see cref="OnChange"/>. It handles what a text field does on a Mac:
/// <list type="bullet">
/// <item>the pointer places the caret, drags a selection, double-clicks a word and triple-clicks a line;</item>
/// <item>arrows move by character, Option+arrows by word, Command+arrows to line and text ends, with Shift extending;</item>
/// <item>Backspace and Delete remove characters, words (Option) or to the line start (Command);</item>
/// <item>Command+A, C, X, V, Z and Shift+Z select all, copy, cut, paste, undo and redo;</item>
/// <item>the caret blinks while focused.</item>
/// </list>
/// Command is Control off macOS. Draws nothing but the text: put it inside a styled container.
/// </summary>
/// <param name="State">The text, selection and composition to show.</param>
/// <param name="OnChange">Called with the state after each edit.</param>
public sealed record TextInput(TextEditState State, Action<TextEditState> OnChange) : Component
{
    private const double BlinkSeconds = 0.53;

    /// <summary>The text's style.</summary>
    public TextStyle Style { get; init; } = TextStyle.Default;

    /// <summary>Whether Enter adds a line (true) or submits (false: a one-line field that scrolls).</summary>
    public bool Multiline { get; init; }

    /// <summary>Shown while the text is empty.</summary>
    public string? Placeholder { get; init; }

    /// <summary>The placeholder's colour.</summary>
    public Vector4 PlaceholderColor { get; init; } = new(0f, 0f, 0f, 0.4f);

    /// <summary>The caret's colour.</summary>
    public Vector4 CaretColor { get; init; } = new(0f, 0f, 0f, 1f);

    /// <summary>The selection's colour.</summary>
    public Vector4 SelectionColor { get; init; } = new(0.2f, 0.4f, 1f, 0.3f);

    /// <summary>Called when Enter is pressed in a one-line input.</summary>
    public Action? OnSubmit { get; init; }

    /// <summary>Called when the input gains (true) or loses (false) focus.</summary>
    public Action<bool>? OnFocusChange { get; init; }

    /// <summary>Whether the text can be selected and copied but not changed.</summary>
    public bool ReadOnly { get; init; }

    /// <summary>Whether it can't be focused or edited.</summary>
    public bool Disabled { get; init; }

    /// <summary>Size and placement.</summary>
    public LayoutStyle Layout { get; init; }

    /// <summary>What assistive technology calls the input.</summary>
    public string? Label { get; init; }

    /// <summary>A handle on the input's box: its bounds, and <see cref="ElementRef.Focus"/> to focus it from code.</summary>
    public ElementRef? Ref { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var text = context.UseRef(new EditableTextRef()).Value;
        var focused = context.UseState(false);
        var blinkOn = context.UseState(true);
        var history = context.UseRef(new EditHistory());
        var drag = context.UseRef<(bool Active, int Anchor)>((false, 0));
        var goalX = context.UseRef<float?>(null);
        var latest = context.UseRef(this);
        latest.Value = this;
        // Several events can arrive before the owner rebuilds the input with the state it was sent
        // (a key and its text in one frame, key repeat, an input method's burst). Until then, each
        // builds on the last state sent, not the stale one in the props; after a rebuild the props
        // win again, so an owner that refuses a change still decides.
        var builds = context.UseRef(0);
        builds.Value++;
        var sent = context.UseRef<(int Build, TextEditState State)?>(null);
        TextEditState Current() => sent.Value is { } last && last.Build == builds.Value ? last.State : latest.Value.State;
        void Emit(TextEditState next)
        {
            sent.Value = (builds.Value, next);
            latest.Value.OnChange(next);
        }
        var clipboard = context.UsePlatform().Clipboard;
        var root = context.Root;
        var isFocused = focused.Value;

        // The caret blinks while focused; any edit or move shows it again (a new state restarts this).
        context.UseEffect(() =>
        {
            blinkOn.Set(true);
            if (!isFocused)
            {
                return null;
            }
            var elapsed = 0.0;
            return root.AddTicker(seconds =>
            {
                elapsed += seconds;
                blinkOn.Set((int)(elapsed / BlinkSeconds) % 2 == 0);
            }).Dispose;
        }, (isFocused, State));

        // While focused, the input is the platform's text input client: typing and input-method
        // composition arrive through it, and the candidate window is placed at its caret.
        var client = context.UseRef<InputClient?>(null);
        client.Value ??= new InputClient();
        var inputClient = client.Value;
        context.UseEffect(() => () =>
        {
            if (ReferenceEquals(root.TextInputClient, inputClient))
            {
                root.TextInputClient = null;
            }
        }, default(ValueTuple));

        void Change(TextEditState next, bool coalesce = false)
        {
            var props = latest.Value;
            if (next == Current())
            {
                return;
            }
            if (next.Text != Current().Text)
            {
                history.Value.Record(Current(), coalesce);
            }
            if (next.Selection.Focus != Current().Selection.Focus || next.Text != Current().Text)
            {
                goalX.Value = null;
            }
            Emit(next);
        }

        bool Editable() => !latest.Value.ReadOnly && !latest.Value.Disabled;

        void OnKey(KeyEventArgs e)
        {
            var props = latest.Value;
            var state = Current();
            if (text.Paragraph is not { } paragraph)
            {
                return;
            }
            var shift = (e.Modifiers & KeyModifiers.Shift) != 0;
            var alt = (e.Modifiers & KeyModifiers.Alt) != 0;
            var command = (e.Modifiers & (OperatingSystem.IsMacOS() ? KeyModifiers.Super : KeyModifiers.Control)) != 0;
            var handled = true;
            switch (e.Key)
            {
                case KeyCode.Left:
                    Change(TextEditing.Move(state, paragraph, command ? CaretMovement.LineStart : alt ? CaretMovement.WordLeft : CaretMovement.Left, shift));
                    break;
                case KeyCode.Right:
                    Change(TextEditing.Move(state, paragraph, command ? CaretMovement.LineEnd : alt ? CaretMovement.WordRight : CaretMovement.Right, shift));
                    break;
                case KeyCode.Up or KeyCode.Down:
                    if (command)
                    {
                        Change(TextEditing.Move(state, paragraph, e.Key == KeyCode.Up ? CaretMovement.DocumentStart : CaretMovement.DocumentEnd, shift));
                        break;
                    }
                    if (!props.Multiline)
                    {
                        handled = false;
                        break;
                    }
                    // Keep the column across short lines: remember the x the first vertical move started from.
                    goalX.Value ??= paragraph.GetCaretRect(state.Selection.FocusPosition).Left;
                    var goal = goalX.Value;
                    Emit(TextEditing.Move(state, paragraph, e.Key == KeyCode.Up ? CaretMovement.Up : CaretMovement.Down, shift, goal));
                    break;
                case KeyCode.Home:
                    Change(TextEditing.Move(state, paragraph, CaretMovement.LineStart, shift));
                    break;
                case KeyCode.End:
                    Change(TextEditing.Move(state, paragraph, CaretMovement.LineEnd, shift));
                    break;
                case KeyCode.Backspace when Editable():
                    Change(command ? TextEditing.DeleteToLineStart(state, paragraph) : TextEditing.DeleteBackward(state, byWord: alt));
                    break;
                case KeyCode.Delete when Editable():
                    Change(TextEditing.DeleteForward(state, byWord: alt));
                    break;
                case KeyCode.Enter or KeyCode.KeypadEnter:
                    if (props.Multiline && Editable())
                    {
                        Change(TextEditing.Insert(state, "\n"));
                    }
                    else if (!props.Multiline && props.OnSubmit is { } submit)
                    {
                        submit();
                    }
                    else
                    {
                        handled = false;
                    }
                    break;
                case KeyCode.A when command:
                    Change(TextEditing.SelectAll(state));
                    break;
                case KeyCode.C when command:
                    if (!state.Selection.IsCollapsed)
                    {
                        clipboard.SetText(state.SelectedText);
                    }
                    break;
                case KeyCode.X when command:
                    if (!state.Selection.IsCollapsed && Editable())
                    {
                        clipboard.SetText(state.SelectedText);
                        Change(TextEditing.Insert(state, ""));
                    }
                    break;
                case KeyCode.V when command:
                    if (Editable() && clipboard.GetText() is { Length: > 0 } pasted)
                    {
                        Change(TextEditing.Insert(state, props.Multiline ? pasted : pasted.ReplaceLineEndings(" ")));
                    }
                    break;
                case KeyCode.Z when command && Editable():
                    if ((shift ? history.Value.Redo(state) : history.Value.Undo(state)) is { } restored)
                    {
                        Emit(restored);
                    }
                    break;
                default:
                    // Keys that type characters arrive as text input; others aren't ours.
                    handled = false;
                    break;
            }
            e.Handled |= handled;
        }

        inputClient.Insert = typed =>
        {
            if (!Editable())
            {
                return;
            }
            var state = Current();
            if (!latest.Value.Multiline)
            {
                typed = typed.Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal);
            }
            if (typed.Length == 0 && state.Composing is null)
            {
                return;
            }
            Change(TextEditing.Insert(state, typed), coalesce: typed.Length == 1 && typed != " " && state.Composing is null);
        };
        inputClient.Mark = (marked, start, length) =>
        {
            if (Editable())
            {
                Emit(TextEditing.SetComposing(Current(), marked, start, length));
            }
        };
        inputClient.Unmark = () =>
        {
            if (Current().Composing is not null)
            {
                Change(TextEditing.EndComposing(Current()));
            }
        };
        inputClient.Caret = () =>
        {
            var state = Current();
            // While composing, the composition's start: the candidate window then stays put as the user types.
            var position = state.Composing is { } composing ? new TextPosition(composing.Start) : state.Selection.FocusPosition;
            var caret = text.CaretRect(position);
            return new System.Drawing.RectangleF(caret.Left, caret.Top, 1f, caret.Height);
        };

        void OnPointerDown(PointerEventArgs e)
        {
            var props = latest.Value;
            if (props.Disabled || e.Button != PointerButton.Left || text.Paragraph is not { } paragraph)
            {
                return;
            }
            var hit = text.HitTest(e.Position);
            var state = Current();
            var next = e.ClickCount switch
            {
                2 => TextEditing.SelectWord(state, paragraph, hit.Index),
                >= 3 => TextEditing.SelectLine(state, paragraph, hit),
                _ when (e.Modifiers & KeyModifiers.Shift) != 0 => state with { Selection = new TextSelection(state.Selection.Anchor, hit.Index, hit.Affinity) },
                _ => state with { Selection = TextSelection.Caret(hit.Index, hit.Affinity), Composing = null },
            };
            drag.Value = (e.ClickCount <= 1, next.Selection.Anchor);
            Change(next);
        }

        void OnPointerMove(PointerEventArgs e)
        {
            if (!drag.Value.Active || text.Paragraph is null)
            {
                return;
            }
            var hit = text.HitTest(e.Position);
            Change(Current() with { Selection = new TextSelection(drag.Value.Anchor, hit.Index, hit.Affinity) });
        }

        return new Box
        {
            Focusable = !Disabled,
            Ref = Ref,
            Semantics = new Semantics { Role = SemanticsRole.TextField, Label = Label ?? Placeholder, Value = State.Text, Disabled = Disabled },
            Layout = Layout,
            Cursor = Radiant.Platform.CursorShape.IBeam,
            OnFocus = _ =>
            {
                focused.Set(true);
                root.TextInputClient = inputClient;
                latest.Value.OnFocusChange?.Invoke(true);
            },
            OnBlur = _ =>
            {
                focused.Set(false);
                if (ReferenceEquals(root.TextInputClient, inputClient))
                {
                    root.TextInputClient = null;
                }
                drag.Value = (false, 0);
                var props = latest.Value;
                if (Current().Composing is not null)
                {
                    Emit(TextEditing.EndComposing(Current()));
                }
                props.OnFocusChange?.Invoke(false);
            },
            OnPointerDown = OnPointerDown,
            OnPointerMove = OnPointerMove,
            OnPointerUp = _ => drag.Value = (false, 0),
            OnKeyDown = OnKey,
            OnTextInput = e =>
            {
                if (!Editable())
                {
                    return;
                }
                // Control characters arrive as keys, not text.
                var typed = e.Text.Replace("\r", "", StringComparison.Ordinal);
                if (!latest.Value.Multiline)
                {
                    typed = typed.Replace("\n", "", StringComparison.Ordinal);
                }
                if (typed.Length > 0 && !char.IsControl(typed[0]))
                {
                    Change(TextEditing.Insert(Current(), typed), coalesce: typed.Length == 1 && typed != " ");
                    e.Handled = true;
                }
            },
            Children =
            [
                new EditableText(State)
                {
                    Ref = text,
                    Style = Style,
                    Multiline = Multiline,
                    Placeholder = Placeholder,
                    PlaceholderColor = PlaceholderColor,
                    CaretColor = CaretColor,
                    SelectionColor = SelectionColor,
                    ShowCaret = isFocused && blinkOn.Value && State.Selection.IsCollapsed && !ReadOnly,
                    Layout = new LayoutStyle { FlexGrow = 1 },
                },
            ],
        };
    }

    /// <summary>The input's face to the platform's input methods, forwarding to the latest build's handlers.</summary>
    private sealed class InputClient : Radiant.Platform.ITextInputClient
    {
        public Action<string> Insert { get; set; } = _ => { };

        public Action<string, int, int> Mark { get; set; } = (_, _, _) => { };

        public Action Unmark { get; set; } = () => { };

        public Func<System.Drawing.RectangleF> Caret { get; set; } = () => default;

        public System.Drawing.RectangleF CaretRect => Caret();

        public void InsertText(string text) => Insert(text);

        public void SetMarkedText(string text, int selectionStart, int selectionLength) => Mark(text, selectionStart, selectionLength);

        public void UnmarkText() => Unmark();
    }

    /// <summary>Undo and redo stacks of whole states, with runs of typing kept as one step.</summary>
    private sealed class EditHistory
    {
        private const int Limit = 200;
        private readonly List<TextEditState> _undo = [];
        private readonly List<TextEditState> _redo = [];
        private bool _coalescing;

        public void Record(TextEditState before, bool coalesce)
        {
            _redo.Clear();
            if (coalesce && _coalescing && _undo.Count > 0)
            {
                return;
            }
            _undo.Add(before);
            if (_undo.Count > Limit)
            {
                _undo.RemoveAt(0);
            }
            _coalescing = coalesce;
        }

        public TextEditState? Undo(TextEditState current)
        {
            _coalescing = false;
            if (_undo.Count == 0)
            {
                return null;
            }
            var previous = _undo[^1];
            _undo.RemoveAt(_undo.Count - 1);
            _redo.Add(current);
            return previous;
        }

        public TextEditState? Redo(TextEditState current)
        {
            _coalescing = false;
            if (_redo.Count == 0)
            {
                return null;
            }
            var next = _redo[^1];
            _redo.RemoveAt(_redo.Count - 1);
            _undo.Add(current);
            return next;
        }
    }
}
