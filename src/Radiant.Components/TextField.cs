using System;
using System.Globalization;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A labelled text field, filled or outlined. The label sits in the field until it's focused or has
/// text, then floats above; the indicator (the bottom line, or the outline) turns to the primary
/// colour and thickens while focused. Supporting text, an error message and a character count sit
/// below it, and icons can lead or trail.
/// <para>
/// Controlled when given <see cref="Value"/> and <see cref="OnChange"/>, otherwise it keeps its own
/// text, starting from <see cref="InitialText"/>.
/// </para>
/// </summary>
/// <param name="Label">The label.</param>
public sealed record TextField(string Label) : Component
{
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

    /// <summary>Shows a count of characters out of this many, under the field.</summary>
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

    /// <summary>Called when Enter is pressed in a one-line field.</summary>
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
        var onChange = OnChange ?? own.Set;
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
        // Resting, the label is centred in the 56 px field; floated, it sits at the top (filled) or on the border (outlined).
        var restingTop = (56f - (body.LineHeight ?? 24f)) / 2f;
        var floatedTop = filled ? 8f : -(small.LineHeight ?? 16f) / 2f;
        var labelTop = restingTop + (floatedTop - restingTop) * floated;
        var fieldTop = filled ? 24f : restingTop;
        var background = filled ? theme.Get(SurfaceName.SurfaceContainerHighest) : theme.SurfaceColor(surface);
        var height = (Multiline ? 88f : 56f) + theme.DensityOffset;
        var leading = LeadingIcon is null ? 16f : 52f;

        var countText = MaxLength is { } max ? string.Create(CultureInfo.InvariantCulture, $"{state.Text.Length}/{max}") : null;
        var under = Error ?? SupportingText;

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
                            Layout = new LayoutStyle { Width = 48, Height = 56, AlignItems = Align.Center, JustifyContent = Justify.Center, Margin = new Edges(4, 0, 0, 0) },
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
                            Layout = new LayoutStyle { Height = 56, AlignItems = Align.Center, JustifyContent = Justify.Center, Margin = new Edges(0, 0, 4, 0) },
                            Children = [Trailing],
                        }
                        : TrailingIcon is null ? null : new Box
                        {
                            Layout = new LayoutStyle { Width = 48, Height = 56, AlignItems = Align.Center, JustifyContent = Justify.Center, Margin = new Edges(0, 0, 4, 0) },
                            Children =
                            [
                                OnTrailingIconPress is null
                                    ? new SurfaceIcon(TrailingIcon) { Legibility = Disabled ? Legibility.Low : Legibility.Medium }
                                    : new IconButton(TrailingIcon, TrailingIconLabel ?? TrailingIcon, IconButtonVariant.Standard) { OnPress = OnTrailingIconPress },
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
}
