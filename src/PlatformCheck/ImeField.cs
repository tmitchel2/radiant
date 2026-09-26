using System;
using Radiant.Layout;
using Radiant.Platform;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.PlatformCheck;

/// <summary>
/// A bare input field: committed text, then the composition in the primary colour and
/// underlined, then a caret. While focused it is the root's text input client, so the input
/// method's candidate window appears under the composition.
/// </summary>
internal sealed record ImeField(ImeFieldModel Model) : Component
{
    public override Element? Build(BuildContext context)
    {
        var theme = context.UseTheme();
        context.Watch(Model.Version);
        var focused = context.UseState(false);
        var fieldRef = context.UseRef(new ElementRef()).Value;
        var model = Model;
        var root = context.Root;
        // Focused from the start, so typing works at once.
        context.UseEffect(() =>
        {
            fieldRef.Focus(visible: false);
            return null;
        }, default(ValueTuple));

        var text = theme.Text(TextType.BodyLarge) with { Color = theme.Get(SurfaceName.SurfaceContainerHighest, on: true) };
        var marked = text with { Color = theme.Get(SurfaceName.Primary) };
        return new Box
        {
            Ref = fieldRef,
            Focusable = true,
            Cursor = CursorShape.IBeam,
            Semantics = new Semantics { Role = SemanticsRole.TextField, Label = "Input method check" },
            Layout = new LayoutStyle
            {
                FlexDirection = FlexDirection.Row,
                AlignItems = Align.Center,
                MinHeight = 48,
                Padding = new Edges(16, 8, 16, 8),
            },
            Background = theme.Get(SurfaceName.SurfaceContainerHighest),
            CornerRadii = theme.Corners(CornerShapeRole.Small),
            BorderWidth = focused.Value ? 2 : 1,
            BorderColor = focused.Value ? theme.Get(SurfaceName.Primary) : theme.Outline,
            OnFocus = _ =>
            {
                focused.Set(true);
                root.TextInputClient = model;
            },
            OnBlur = _ =>
            {
                focused.Set(false);
                if (ReferenceEquals(root.TextInputClient, model))
                {
                    root.TextInputClient = null;
                }
            },
            OnKeyDown = e =>
            {
                if (e.Key == KeyCode.Backspace)
                {
                    model.Backspace();
                    e.Handled = true;
                }
                else if (e.Key is KeyCode.Enter or KeyCode.Escape or KeyCode.Space
                    or KeyCode.Left or KeyCode.Right or KeyCode.Up or KeyCode.Down)
                {
                    // Keys an input method uses while composing: logged when the UI gets them, which
                    // it shouldn't mid-composition.
                    model.KeyReceived(e.Key.ToString());
                }
            },
            Children =
            [
                new Box { Ref = model.TextRef, Children = [new TextBlock(model.Text) { Style = text, Wrap = false }] },
                model.Marked.Length == 0 ? null : new Box
                {
                    Children =
                    [
                        new TextBlock(model.Marked) { Style = marked, Wrap = false },
                        new Box { Layout = new LayoutStyle { Height = 2 }, Background = theme.Get(SurfaceName.Primary) },
                    ],
                },
                focused.Value && model.Marked.Length == 0
                    ? new Box { Layout = new LayoutStyle { Width = 2, Height = 22 }, Background = theme.Get(SurfaceName.Primary) }
                    : null,
            ],
        };
    }
}
