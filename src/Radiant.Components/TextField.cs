using System;
using System.Globalization;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A labelled text field, filled or outlined. The label sits in the field until it's focused or has
/// text, then floats above; the indicator (the bottom line, or the outline) turns to the primary
/// colour and thickens while focused. In a theme whose fields put the label above
/// (<see cref="FieldLook.LabelAbove"/>), the label sits over a plain bordered input instead, whose
/// border turns to the primary colour, ringed, while focused. Supporting text, an error message and
/// a character count sit below it, and icons can lead or trail.
/// <para>
/// Controlled when given <see cref="Value"/> and <see cref="OnChange"/>, otherwise it keeps its own
/// text, starting from <see cref="InitialText"/>.
/// </para>
/// </summary>
/// <param name="Label">The label.</param>
[RequiresTestId]
public sealed partial record TextField(string Label) : Component
{
    [TestId] public static partial string Input { get; }
    [TestId<IconButton>] public static partial string TrailingButton { get; }

    /// <summary>The text, selection and composition, for a controlled field.</summary>
    public TextEditState? Value { get; init; }

    /// <summary>Called after each edit.</summary>
    public Action<TextEditState>? OnChange { get; init; }

    /// <summary>The starting text of an uncontrolled field.</summary>
    public string InitialText { get; init; } = "";

    /// <summary>Filled or outlined.</summary>
    public TextFieldVariant Variant { get; init; }

    /// <summary>Help shown under the field.</summary>
    public string? SupportingText { get; init; }

    /// <summary>An error, shown instead of the supporting text, turning the field to the error colour.</summary>
    public string? Error { get; init; }

    /// <summary>
    /// The most characters it takes: a count of them out of this many shows under the field, and an edit
    /// that would take the text past it is cut to fit (an input method's unfinished text excepted).
    /// </summary>
    public int? MaxLength { get; init; }

    /// <summary>A leading icon.</summary>
    public string? LeadingIcon { get; init; }

    /// <summary>A trailing icon.</summary>
    public string? TrailingIcon { get; init; }

    /// <summary>Anything at the field's end in place of <see cref="TrailingIcon"/> (a number field's stepper).</summary>
    public Element? Trailing { get; init; }

    /// <summary>What pressing the trailing icon does (clear the text, reveal a password).</summary>
    public Action? OnTrailingIconPress { get; init; }

    /// <summary>What assistive technology calls the trailing icon's button ("Show password"); the icon's name if null.</summary>
    public string? TrailingIconLabel { get; init; }

    /// <summary>Text shown while focused and empty.</summary>
    public string? Placeholder { get; init; }

    /// <summary>Whether Enter adds a line.</summary>
    public bool Multiline { get; init; }

    /// <summary>Called when Enter is pressed in a one-line field, or ⌘Enter (Ctrl+Enter) in a multi-line one.</summary>
    public Action? OnSubmit { get; init; }

    /// <summary>Called with true when the field takes focus and false when it loses it.</summary>
    public Action<bool>? OnFocusChange { get; init; }

    /// <summary>A handle to the field's text input, to focus it from outside.</summary>
    public ElementRef? InputRef { get; init; }

    /// <summary>Whether it can't be used.</summary>
    public bool Disabled { get; init; }

    /// <summary>Whether its text can be selected but not changed (a select's display).</summary>
    public bool ReadOnly { get; init; }

    /// <summary>Size and placement of the whole field.</summary>
    public LayoutStyle? Layout { get; init; }

    /// <inheritdoc/>
    // An edit that would take the text past the limit, cut to fit: what was inserted is shortened, and
    // the caret goes after what's kept of it. Composing text isn't cut, so an input method can finish.
    internal static TextEditState Limit(TextEditState old, TextEditState next, int limit)
    {
        if (next.Text.Length <= limit || next.Text.Length <= old.Text.Length || next.Composing is not null)
        {
            return next;
        }
        var shorter = Math.Min(old.Text.Length, next.Text.Length);
        var prefix = 0;
        while (prefix < shorter && old.Text[prefix] == next.Text[prefix])
        {
            prefix++;
        }
        var suffix = 0;
        while (suffix < shorter - prefix && old.Text[^(suffix + 1)] == next.Text[^(suffix + 1)])
        {
            suffix++;
        }
        var inserted = next.Text.Length - prefix - suffix;
        var room = Math.Max(0, limit - (next.Text.Length - inserted));
        var kept = Math.Min(room, inserted);
        var text = next.Text[..(prefix + kept)] + next.Text[^suffix..];
        return new TextEditState(text, TextSelection.Caret(prefix + kept));
    }

    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var surface = context.UseSurface();
        var own = context.UseState(() => TextEditState.From(InitialText));
        var focused = context.UseState(false);
        var ownInput = context.UseRef(new ElementRef()).Value;
        var input = InputRef ?? ownInput;
        var focusChange = OnFocusChange;
        var state = Value ?? own.Value;
        var change = OnChange ?? own.Set;
        var maxLength = MaxLength;
        Action<TextEditState> onChange = maxLength is { } limit ? next => change(Limit(state, next, limit)) : change;
        var error = Error is not null;
        var filled = Variant == TextFieldVariant.Filled;

        var motion = theme.Theme.Motion;
        var floated = context.UseTransition(focused.Value || state.Text.Length > 0 ? 1f : 0f,
            motion.Reduced ? TimeSpan.Zero : motion.ShortDuration, motion.Standard);

        // Colours: primary while focused, error when in error, faded when disabled.
        var faded = surface with { Content = surface.Content with { Opacity = Legibility.Low } };
        var accent = error ? theme.Get(SurfaceName.Error) : theme.Get(SurfaceName.Primary);
        var quiet = theme.Get(SurfaceName.SurfaceVariant, on: true);
        var labelColor = Disabled ? theme.ContentColor(faded) : error ? accent : focused.Value ? accent : quiet;
        var indicator = Disabled ? theme.ContentColor(faded) : error || focused.Value ? accent : filled ? quiet : theme.Outline;
        var textColor = Disabled ? theme.ContentColor(faded) : theme.Get(SurfaceName.Surface, on: true);
        var indicatorWidth = focused.Value && !Disabled ? 2f : 1f;

        var body = theme.Text(TextType.BodyLarge);
        var small = theme.Text(TextType.BodySmall);
        var labelSize = body.Size + (small.Size - body.Size) * floated;
        // A line of the field: 56 px, less at a compact density.
        var line = 56f + theme.DensityOffset;
        // Resting, the label is centred in the line; floated, it sits at the top (filled) or on the border (outlined).
        var restingTop = (line - (body.LineHeight ?? 24f)) / 2f;
        var floatedTop = filled ? 8f + theme.DensityOffset / 4f : -(small.LineHeight ?? 16f) / 2f;
        var labelTop = restingTop + (floatedTop - restingTop) * floated;
        var fieldTop = filled ? 24f + theme.DensityOffset / 2f : restingTop;
        var background = filled ? theme.Get(SurfaceName.SurfaceContainerHighest) : theme.SurfaceColor(surface);
        var height = (Multiline ? 88f : 56f) + theme.DensityOffset;
        var leading = LeadingIcon is null ? 16f : 52f;

        var countText = MaxLength is { } max ? string.Create(CultureInfo.InvariantCulture, $"{state.Text.Length}/{max}") : null;
        var under = Error ?? SupportingText;
        var style = theme.Theme.Components.Field;
        if (Variant == TextFieldVariant.Plain)
        {
            return Plain(theme, style, surface, state, onChange, input, focused, focusChange);
        }
        if (style.Look == FieldLook.LabelAbove)
        {
            return LabelAbove(theme, style, surface, state, onChange, input, focused, focusChange, countText, under);
        }

        return new Box
        {
            Layout = new LayoutStyle { MinWidth = 210, AlignSelf = Align.Stretch }.Merge(Layout ?? default),
            Children =
            [
                new Box
                {
                    OnPointerDown = _ => input.Focus(visible: false),
                    Layout = new LayoutStyle
                    {
                        FlexDirection = FlexDirection.Row,
                        AlignItems = Align.FlexStart,
                        MinHeight = height,
                    },
                    Background = filled ? background : null,
                    CornerRadii = filled
                        ? Radiant.Graphics2D.CornerRadii.Top(theme.Radius(CornerShapeRole.ExtraSmall))
                        : theme.Corners(CornerShapeRole.ExtraSmall),
                    BorderWidth = filled ? 0f : indicatorWidth,
                    BorderColor = indicator,
                    Children =
                    [
                        LeadingIcon is null ? null : new Box
                        {
                            Layout = new LayoutStyle { Width = 48, Height = line, AlignItems = Align.Center, JustifyContent = Justify.Center, Margin = new Edges(4, 0, 0, 0) },
                            Children = [new SurfaceIcon(LeadingIcon) { Legibility = Disabled ? Legibility.Low : Legibility.Medium }],
                        },
                        new Box
                        {
                            Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1, MinHeight = height, Margin = new Edges(LeadingIcon is null ? 16 : 0, 0, TrailingIcon is null ? 16 : 0, 0) },
                            Children =
                            [
                                // The label's backing cuts the outline where the label sits on it.
                                new Box
                                {
                                    HitTestVisible = false,
                                    Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = new Edges(-4, labelTop, Dimension.Undefined, Dimension.Undefined), Padding = Edges.Symmetric(4, 0) },
                                    Background = filled || floated <= 0f ? null : background,
                                    // The input carries the label for assistive technology.
                                    Children = [new TextBlock(Label) { IsDecorative = true, Style = body with { Size = labelSize, LineHeight = null, Color = labelColor }, Wrap = false }],
                                },
                                new TextInput(state, onChange)
                                {
                                    TestId = Input,
                                    Ref = input,
                                    Label = Label,
                                    Disabled = Disabled,
                                    ReadOnly = ReadOnly,
                                    Multiline = Multiline,
                                    OnSubmit = OnSubmit,
                                    Placeholder = focused.Value ? Placeholder : null,
                                    PlaceholderColor = theme.ContentColor(faded),
                                    Style = body with { Color = textColor },
                                    CaretColor = accent,
                                    SelectionColor = accent with { A = 0.3f },
                                    OnFocusChange = f =>
                                    {
                                        focused.Set(f);
                                        focusChange?.Invoke(f);
                                    },
                                    Layout = new LayoutStyle { Margin = new Edges(0, fieldTop, 0, 8), MinHeight = body.LineHeight ?? 24f },
                                },
                            ],
                        },
                        Trailing is not null ? new Box
                        {
                            Layout = new LayoutStyle { Height = line, AlignItems = Align.Center, JustifyContent = Justify.Center, Margin = new Edges(0, 0, 4, 0) },
                            Children = [Trailing],
                        }
                        : TrailingIcon is null ? null : new Box
                        {
                            Layout = new LayoutStyle { Width = 48, Height = line, AlignItems = Align.Center, JustifyContent = Justify.Center, Margin = new Edges(0, 0, 4, 0) },
                            Children =
                            [
                                OnTrailingIconPress is null
                                    ? new SurfaceIcon(TrailingIcon) { Legibility = Disabled ? Legibility.Low : Legibility.Medium }
                                    : new IconButton(TrailingIcon, TrailingIconLabel ?? TrailingIcon, IconButtonVariant.Standard) { TestId = TrailingButton, OnPress = OnTrailingIconPress },
                            ],
                        },
                        !filled ? null : new Box
                        {
                            HitTestVisible = false,
                            Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = new Edges(0, Dimension.Undefined, 0, 0), Height = indicatorWidth },
                            Background = indicator,
                        },
                    ],
                },
                under is null && countText is null ? null : new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, Padding = new Edges(16, 4, 16, 0), ColumnGap = 16 },
                    Children =
                    [
                        new TextBlock(under ?? "") { Style = small with { Color = error ? accent : quiet }, Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 } },
                        countText is null ? null : new TextBlock(countText) { Style = small with { Color = quiet } },
                    ],
                },
            ],
        };
    }

    // The label above a plain bordered input; supporting text, error and count below.
    private Box LabelAbove(ResolvedTheme theme, FieldStyle style, SurfaceState surface, TextEditState state, Action<TextEditState> onChange,
        ElementRef input, State<bool> focused, Action<bool>? focusChange, string? countText, string? under)
    {
        var error = Error is not null;
        var recolor = Disabled && theme.RecolorsDisabled();
        var interaction = theme.Theme.Components.Interaction;
        var faded = surface with { Content = surface.Content with { Opacity = Legibility.Low } };
        var accent = error ? theme.Get(SurfaceName.Error) : theme.Get(SurfaceName.Primary);
        var quiet = theme.Get(SurfaceName.SurfaceVariant, on: true);
        var ink = recolor ? theme.ContentColor(faded) : theme.Get(SurfaceName.Surface, on: true);
        var active = focused.Value && !Disabled;
        var border = recolor ? theme.ContentColor(faded) : error || active ? accent : theme.Outline;
        var body = theme.Text(style.Text);
        var small = theme.Text(TextType.BodySmall);
        var height = (Multiline ? style.Height * 2f : style.Height) + theme.DensityOffset;
        var radius = theme.Radius(style.Shape);
        var filled = Variant == TextFieldVariant.Filled;
        const float ring = 3f;
        return new Box
        {
            Opacity = Disabled && !recolor ? interaction.DisabledOpacity : 1f,
            Layout = new LayoutStyle { MinWidth = 210, AlignSelf = Align.Stretch, RowGap = 6 }.Merge(Layout ?? default),
            Children =
            [
                // The input carries the label for assistive technology.
                new TextBlock(Label) { IsDecorative = true, Wrap = false, Style = theme.Text(style.Label) with { Color = error ? accent : ink } },
                new Box
                {
                    OnPointerDown = _ => input.Focus(visible: false),
                    Layout = new LayoutStyle
                    {
                        FlexDirection = FlexDirection.Row,
                        AlignItems = Multiline ? Align.FlexStart : Align.Center,
                        MinHeight = height,
                        Padding = new Edges(LeadingIcon is null ? 12 : 10, 0, TrailingIcon is null && Trailing is null ? 12 : 4, 0),
                        ColumnGap = 8,
                    },
                    Background = filled ? theme.Get(SurfaceName.SurfaceContainer) : theme.Get(SurfaceName.SurfaceBright),
                    CornerRadii = Radiant.Graphics2D.CornerRadii.All(radius),
                    BorderWidth = 1f,
                    BorderColor = filled && !active && !error ? theme.Get(SurfaceName.SurfaceContainer) : border,
                    Children =
                    [
                        // Focus rings the input softly in the accent.
                        !active ? null : new Box
                        {
                            HitTestVisible = false,
                            Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = Edges.All(-1f - ring) },
                            BorderWidth = ring,
                            BorderColor = accent with { A = 0.25f },
                            CornerRadii = Radiant.Graphics2D.CornerRadii.All(radius + ring + 1f),
                        },
                        LeadingIcon is null ? null : new SurfaceIcon(LeadingIcon) { IconSize = 18, Legibility = Legibility.Medium },
                        new TextInput(state, onChange)
                        {
                            TestId = Input,
                            Ref = input,
                            Label = Label,
                            Disabled = Disabled,
                            ReadOnly = ReadOnly,
                            Multiline = Multiline,
                            OnSubmit = OnSubmit,
                            Placeholder = Placeholder,
                            PlaceholderColor = theme.ContentColor(faded),
                            Style = body with { Color = ink },
                            CaretColor = accent,
                            SelectionColor = accent with { A = 0.3f },
                            OnFocusChange = f =>
                            {
                                focused.Set(f);
                                focusChange?.Invoke(f);
                            },
                            Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1, MinHeight = body.LineHeight ?? 20f, Margin = Multiline ? Edges.Symmetric(0, 8) : Edges.None },
                        },
                        Trailing ?? (TrailingIcon is null ? null
                            : OnTrailingIconPress is null
                                ? new SurfaceIcon(TrailingIcon) { IconSize = 18, Legibility = Legibility.Medium, Layout = new LayoutStyle { Margin = new Edges(0, 0, 8, 0) } }
                                : new IconButton(TrailingIcon, TrailingIconLabel ?? TrailingIcon, IconButtonVariant.Standard) { TestId = TrailingButton, OnPress = OnTrailingIconPress, Layout = new LayoutStyle { Width = 28, Height = 28 } }),
                    ],
                },
                under is null && countText is null ? null : new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, ColumnGap = 16 },
                    Children =
                    [
                        new TextBlock(under ?? "") { Style = small with { Color = error ? accent : quiet }, Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 } },
                        countText is null ? null : new TextBlock(countText) { Style = small with { Color = quiet } },
                    ],
                },
            ],
        };
    }

    // Just the text input, framed by whatever holds it.
    private TextInput Plain(ResolvedTheme theme, FieldStyle style, SurfaceState surface, TextEditState state, Action<TextEditState> onChange,
        ElementRef input, State<bool> focused, Action<bool>? focusChange)
    {
        var faded = surface with { Content = surface.Content with { Opacity = Legibility.Low } };
        var accent = theme.Get(SurfaceName.Primary);
        return new TextInput(state, onChange)
        {
            TestId = Input,
            Ref = input,
            Label = Label,
            Disabled = Disabled,
            ReadOnly = ReadOnly,
            Multiline = Multiline,
            OnSubmit = OnSubmit,
            Placeholder = Placeholder,
            PlaceholderColor = theme.ContentColor(faded),
            Style = theme.Text(style.Text) with { Color = Disabled ? theme.ContentColor(faded) : theme.ContentColor(surface) },
            CaretColor = accent,
            SelectionColor = accent with { A = 0.3f },
            OnFocusChange = f =>
            {
                focused.Set(f);
                focusChange?.Invoke(f);
            },
            Layout = new LayoutStyle { AlignSelf = Align.Stretch, MinHeight = theme.Text(style.Text).LineHeight ?? 20f }.Merge(Layout ?? default),
        };
    }
}
