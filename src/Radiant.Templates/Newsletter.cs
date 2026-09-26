using System;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>
/// A sign-up for news by email: a headline and sentence beside an email field and a Subscribe
/// button. The address is checked before <paramref name="OnSubscribe"/> is called, and a thank-you
/// replaces the field once it has been.
/// </summary>
/// <param name="Headline">The headline ("Stay up to date").</param>
/// <param name="OnSubscribe">Called with the address.</param>
public sealed record Newsletter(string Headline, Action<string>? OnSubscribe) : Component
{
    /// <summary>A sentence under the headline.</summary>
    public string? Text { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var form = context.UseForm();
        var email = form.Field("email", "", Validators.Required("Enter your email"), Validators.Email());
        var done = context.UseState(false);
        var subscribe = OnSubscribe;
        var success = context.UseSurface().With(new SurfaceChange { Content = SurfaceName.Success });
        void Submit() => form.Submit(values =>
        {
            subscribe?.Invoke(values["email"]);
            done.Set(true);
        });
        return new Box
        {
            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, FlexWrap = FlexWrap.Wrap, AlignItems = Align.Center, ColumnGap = 32, RowGap = 16, Padding = Edges.Symmetric(0, 16) },
            Children =
            [
                new Box
                {
                    Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1, FlexBasis = 280, RowGap = 8 },
                    Children =
                    [
                        new SurfaceText(Headline) { TextType = TextType.HeadlineSmall, HeadingLevel = 2 },
                        Text is null ? null : new SurfaceText(Text) { Legibility = Legibility.Medium },
                    ],
                },
                done.Value
                    ? new Box
                    {
                        Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 8, FlexBasis = 280, FlexGrow = 1 },
                        Children = [ThemeContexts.Surface.Provide(success, new SurfaceIcon("check_circle")), new SurfaceText("Thanks! Check your inbox to confirm.")],
                    }
                    : new Box
                    {
                        Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.FlexStart, ColumnGap = 8, FlexBasis = 280, FlexGrow = 1 },
                        Children =
                        [
                            new TextField("Email address")
                            {
                                Value = email.State,
                                OnChange = email.Set,
                                OnFocusChange = email.FocusChanged,
                                InputRef = email.InputRef,
                                Error = email.Error,
                                Variant = TextFieldVariant.Outlined,
                                OnSubmit = Submit,
                                Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 },
                            },
                            new SurfaceButton("Subscribe") { OnPress = Submit, Layout = new LayoutStyle { Margin = new Edges(0, 8, 0, 0) } },
                        ],
                    },
            ],
        };
    }
}
