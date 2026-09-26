using System;
using System.Collections.Generic;
using System.Linq;
using Radiant.Components.Primitives;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A text field that suggests options as you type: a list drops from it, ranked by
/// <see cref="FuzzyMatch"/>, and keyboard focus stays in the field throughout. Down opens the list
/// and moves the highlight, Up moves it back, Enter or a press picks, and Escape closes the list
/// and puts back the text. Leaving the field keeps typed text only with
/// <see cref="AllowCustom"/>; otherwise it goes back to the chosen option. Controlled: shows
/// <paramref name="Value"/> and reports each choice.
/// </summary>
/// <param name="Label">The field's label.</param>
/// <param name="Options">The suggestions.</param>
/// <param name="Value">The chosen value, or null for none.</param>
/// <param name="OnChange">Called with the new value.</param>
[RequiresTestId]
public sealed partial record ComboBox(string Label, IReadOnlyList<string> Options, string? Value, Action<string?>? OnChange) : Component
{
    [TestId<TextField>] public static partial string Field { get; }

    private const float RowHeight = 40f;

    /// <summary>Whether text that matches no option is kept as the value.</summary>
    public bool AllowCustom { get; init; }

    /// <summary>Filled or outlined, as <see cref="TextField"/>.</summary>
    public TextFieldVariant Variant { get; init; }

    /// <summary>Help under the field.</summary>
    public string? SupportingText { get; init; }

    /// <summary>An icon at the field's start.</summary>
    public string? LeadingIcon { get; init; }

    /// <summary>The most options the list shows before it scrolls.</summary>
    public int MaxVisible { get; init; } = 6;

    /// <summary>What the list says when nothing matches (with <see cref="AllowCustom"/> off).</summary>
    public string EmptyText { get; init; } = "No matches";

    /// <summary>The field's size and placement.</summary>
    public LayoutStyle? Layout { get; init; }

    /// <summary>The options a typed text suggests, best first; all of them for no text or the chosen value.</summary>
    public static IReadOnlyList<string> Suggest(IReadOnlyList<string> options, string text, string? value)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(text);
        text = text.Trim();
        if (text.Length == 0 || text == value)
        {
            return options;
        }
        return
        [
            .. options
                .Select((option, order) => (option, order, score: FuzzyMatch.Score(text, option)))
                .Where(r => r.score is not null)
                .OrderByDescending(r => r.score)
                .ThenBy(r => r.order)
                .Select(r => r.option),
        ];
    }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var text = context.UseState(() => TextEditState.From(Value ?? ""));
        var open = context.UseState(false);
        var highlight = context.UseState(0);
        var anchor = context.UseRef(new ElementRef()).Value;
        var input = context.UseRef(new ElementRef()).Value;
        var scroll = context.UseRef(new Radiant.Scrolling.ScrollController(new Radiant.Scrolling.ScrollBehaviour())).Value;
        var latest = context.UseRef(this);
        latest.Value = this;

        // The field shows the value whenever it changes from outside.
        var value = Value;
        context.UseEffect(() =>
        {
            if (text.Value.Text != (value ?? ""))
            {
                text.Set(TextEditState.From(value ?? ""));
            }
            return null;
        }, value);

        var shown = Suggest(Options, text.Value.Text, Value);
        var at = shown.Count == 0 ? -1 : Math.Clamp(highlight.Value, 0, shown.Count - 1);

        void Choose(string? chosen)
        {
            var props = latest.Value;
            text.Set(TextEditState.From(chosen ?? ""));
            open.Set(false);
            if (chosen != props.Value)
            {
                props.OnChange?.Invoke(chosen);
            }
        }

        // Typed text stays only if it's allowed or is an option; otherwise the value comes back.
        void Settle()
        {
            var props = latest.Value;
            var typed = text.Value.Text.Trim();
            var exact = props.Options.FirstOrDefault(o => string.Equals(o, typed, StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
            {
                Choose(exact);
            }
            else if (props.AllowCustom)
            {
                Choose(typed.Length == 0 ? null : typed);
            }
            else
            {
                Choose(typed.Length == 0 ? null : props.Value);
            }
        }

        void Move(int to)
        {
            if (!open.Value)
            {
                open.Set(true);
                highlight.Set(0);
                return;
            }
            if (shown.Count > 0)
            {
                var next = Math.Clamp(to, 0, shown.Count - 1);
                highlight.Set(next);
                scroll.ScrollIntoView(next * RowHeight, RowHeight);
            }
        }

        var rows = new List<Element?>();
        for (var i = 0; i < shown.Count; i++)
        {
            var index = i;
            var option = shown[i];
            rows.Add(new Option(option, i == at, option == Value, () => highlight.Set(index), () =>
            {
                Choose(option);
                input.Focus(visible: false);
            })
            { Key = option });
        }

        return new Fragment(
            new Box
            {
                Ref = anchor,
                Layout = new LayoutStyle { AlignSelf = Align.Stretch }.Merge(Layout ?? default),
                OnKeyDownCapture = e =>
                {
                    switch (e.Key)
                    {
                        case KeyCode.Down: Move(at + 1); e.Handled = true; break;
                        case KeyCode.Up when open.Value: Move(at - 1); e.Handled = true; break;
                        case KeyCode.Enter when open.Value && at >= 0: Choose(shown[at]); e.Handled = true; break;
                        case KeyCode.Enter: Settle(); e.Handled = true; break;
                        case KeyCode.Escape when open.Value:
                            open.Set(false);
                            text.Set(TextEditState.From(latest.Value.Value ?? ""));
                            e.Handled = true;
                            break;
                        default: break;
                    }
                },
                Children =
                [
                    new TextField(Label)
                    {
                        TestId = Field,
                        Value = text.Value,
                        OnChange = next =>
                        {
                            text.Set(next);
                            open.Set(true);
                            highlight.Set(0);
                        },
                        InputRef = input,
                        Variant = Variant,
                        SupportingText = SupportingText,
                        LeadingIcon = LeadingIcon,
                        TrailingIcon = open.Value ? "arrow_drop_up" : "arrow_drop_down",
                        TrailingIconLabel = open.Value ? $"Hide {Label} options" : $"Show {Label} options",
                        OnTrailingIconPress = () =>
                        {
                            open.Set(!open.Value);
                            highlight.Set(0);
                            input.Focus(visible: false);
                        },
                        OnFocusChange = focused =>
                        {
                            if (!focused)
                            {
                                Settle();
                            }
                        },
                    },
                ],
            },
            !open.Value ? null : new Anchored(anchor, new Surface
            {
                SurfaceColor = SurfaceName.SurfaceContainer,
                CornerShape = CornerShapeRole.ExtraSmall,
                Elevation = ElevationLevel.Level2,
                ClipContent = true,
                Semantics = new Semantics { Role = SemanticsRole.List, Label = $"{Label} options" },
                Layout = new LayoutStyle { MaxHeight = MaxVisible * RowHeight + 16 },
                Children =
                [
                    shown.Count == 0
                        ? new SurfaceText(AllowCustom ? $"Use “{text.Value.Text.Trim()}”" : EmptyText)
                        {
                            Legibility = Legibility.Medium,
                            Layout = new LayoutStyle { Padding = Edges.Symmetric(16, 12) },
                        }
                        : new ScrollArea
                        {
                            Controller = scroll,
                            Layout = new LayoutStyle { FlexShrink = 1 },
                            ContentLayout = new LayoutStyle { Padding = Edges.Symmetric(0, 8) },
                            Children = rows,
                        },
                ],
            })
            { MatchAnchorWidth = true, Offset = 2f });
    }

    /// <summary>An option: its text, ticked when it's the value, filled while highlighted.</summary>
    private sealed record Option(string Text, bool Highlighted, bool Chosen, Action Highlight, Action Pick) : Component
    {
        public override Element? Build(BuildContext context)
        {
            var theme = context.UseTheme();
            var state = context.UseSurface();
            var (highlight, pick, highlighted) = (Highlight, Pick, Highlighted);
            return new Box
            {
                Semantics = new Semantics { Role = SemanticsRole.ListItem, Label = Text, Selected = Highlighted, Checked = Chosen },
                Background = Highlighted ? theme.StateLayerColor(state, theme.Theme.StateLayers.Hover * 1.5f) : null,
                Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 12, Height = RowHeight, Padding = Edges.Symmetric(16, 0) },
                OnPointerMove = _ =>
                {
                    if (!highlighted)
                    {
                        highlight();
                    }
                },
                // Picked on press: the field loses focus as the press lands here, and this runs after.
                OnPointerDown = e =>
                {
                    if (e.Button == PointerButton.Left)
                    {
                        pick();
                        e.Handled = true;
                    }
                },
                Children =
                [
                    new SurfaceText(Text) { TextType = TextType.BodyLarge, MaxLines = 1, Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 } },
                    Chosen ? new SurfaceIcon("check") { IconSize = 18 } : null,
                ],
            };
        }
    }
}
