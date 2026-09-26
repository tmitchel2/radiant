using System;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A compact search box (36 px, for toolbars, lists and headers): a search icon, the text, and a
/// clear button once there's text. Escape clears it (and a second Escape leaves it to whatever is
/// around it), Enter submits. Controlled through <see cref="Value"/> and <see cref="OnChange"/>, or
/// keeping its own text.
/// </summary>
/// <param name="Placeholder">What it says while empty ("Search mail").</param>
[RequiresTestId]
public sealed partial record SearchField(string Placeholder) : Component
{
    [TestId<IconButton>] public static partial string Clear { get; }

    /// <summary>The text, when the owner keeps it; null for the field to keep it.</summary>
    public TextEditState? Value { get; init; }

    /// <summary>Called with each change.</summary>
    public Action<TextEditState>? OnChange { get; init; }

    /// <summary>Called with the text when Enter is pressed.</summary>
    public Action<string>? OnSubmit { get; init; }

    /// <summary>A handle to the text input, to focus it from outside (a ⌘F shortcut).</summary>
    public ElementRef? InputRef { get; init; }

    /// <summary>The field's size and placement (it stretches across its parent by default).</summary>
    public LayoutStyle? Layout { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var own = context.UseState(TextEditState.Empty);
        var focused = context.UseState(false);
        var ownInput = context.UseRef(new ElementRef()).Value;
        var input = InputRef ?? ownInput;
        var state = Value ?? own.Value;
        var (value, onChange, submit) = (Value, OnChange, OnSubmit);

        void Change(TextEditState next)
        {
            if (value is null)
            {
                own.Set(next);
            }
            onChange?.Invoke(next);
        }

        var surface = context.UseSurface().With(new SurfaceChange { Surface = SurfaceName.SurfaceContainerHigh });
        var faded = surface with { Content = surface.Content with { Opacity = Legibility.Medium } };
        var accent = theme.Get(SurfaceName.Primary);
        return ThemeContexts.Surface.Provide(surface, new Box
        {
            Semantics = new Semantics { Role = SemanticsRole.Group, Label = Placeholder },
            Background = theme.SurfaceColor(surface),
            BorderWidth = focused.Value ? 2f : 0f,
            BorderColor = accent,
            CornerRadii = theme.Corners(CornerShapeRole.Control),
            Layout = new LayoutStyle
            {
                FlexDirection = FlexDirection.Row,
                AlignItems = Align.Center,
                Height = 36 + theme.DensityOffset / 2f,
                Padding = new Edges(12, 0, 4, 0),
                ColumnGap = 8,
                MinWidth = 160,
                AlignSelf = Align.Stretch,
            }.Merge(Layout ?? default),
            OnClick = _ => input.Focus(visible: false),
            OnKeyDownCapture = e =>
            {
                if (e.Key == KeyCode.Escape && state.Text.Length > 0)
                {
                    Change(TextEditState.Empty);
                    e.Handled = true;
                }
            },
            Children =
            [
                new SurfaceIcon("search") { IconSize = 18, Legibility = Legibility.Medium },
                new TextInput(state, Change)
                {
                    Ref = input,
                    Label = Placeholder,
                    Placeholder = Placeholder,
                    Style = theme.Text(TextType.BodyMedium) with { Color = theme.ContentColor(surface) },
                    PlaceholderColor = theme.ContentColor(faded),
                    CaretColor = accent,
                    SelectionColor = accent with { A = 0.3f },
                    OnSubmit = () => submit?.Invoke(state.Text),
                    OnFocusChange = focused.Set,
                    Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 },
                },
                state.Text.Length == 0 ? new Box { Layout = new LayoutStyle { Width = 28 } } : new IconButton("close", "Clear search")
                {
                    TestId = Clear,
                    OnPress = () =>
                    {
                        Change(TextEditState.Empty);
                        input.Focus(visible: false);
                    },
                    Layout = new LayoutStyle { Width = 28, Height = 28 },
                },
            ],
        });
    }
}
