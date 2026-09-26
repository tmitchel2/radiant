using System;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>
/// A sign-in card: email and password fields (the password hidden, with a reveal button), remember
/// me, a forgotten-password link and the sign-in button. Reports the entered details on submit.
/// </summary>
/// <param name="OnSignIn">Called with the email, password and remember-me choice.</param>
public sealed partial record SignInForm(Action<string, string, bool> OnSignIn) : Component
{
    [TestId<TextField>] public static partial string Email { get; }
    [TestId<TextField>] public static partial string Password { get; }
    [TestId<Checkbox>] public static partial string RememberMe { get; }
    [TestId<SurfaceButton>] public static partial string ForgotPassword { get; }
    [TestId<SurfaceButton>] public static partial string SignIn { get; }

    /// <summary>The heading.</summary>
    public string Title { get; init; } = "Sign in to your account";

    /// <summary>An error to show, such as "Wrong password".</summary>
    public string? Error { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var form = context.UseForm();
        var email = form.Field("email", "", Validators.Required("Enter your email"), Validators.Email());
        var password = form.Field("password", "", Validators.Required("Enter your password"));
        var reveal = context.UseState(false);
        var remember = context.UseState(true);
        var signIn = OnSignIn;
        // Nothing is signed in with until both fields pass; the first that doesn't takes focus.
        void Submit() => form.Submit(values => signIn(values["email"], values["password"], remember.Value));
        // Hidden, the field shows a dot per character; the real text lives in the form.
        var shown = reveal.Value ? password.State : password.State with { Text = new string('•', password.Text.Length) };
        return new Card(
            new SurfaceText(Title) { TextType = TextType.HeadlineSmall },
            new TextField("Email")
            {
                TestId = Email,
                Value = email.State,
                OnChange = email.Set,
                OnFocusChange = email.FocusChanged,
                InputRef = email.InputRef,
                Error = email.Error,
                LeadingIcon = "mail",
                Variant = TextFieldVariant.Outlined,
                OnSubmit = Submit,
            },
            new TextField("Password")
            {
                TestId = Password,
                Value = shown,
                OnChange = next => password.Set(reveal.Value ? next : Unmask(password.State, next)),
                OnFocusChange = password.FocusChanged,
                InputRef = password.InputRef,
                LeadingIcon = "lock",
                TrailingIcon = reveal.Value ? "visibility_off" : "visibility",
                OnTrailingIconPress = () => reveal.Set(!reveal.Value),
                TrailingIconLabel = reveal.Value ? "Hide password" : "Show password",
                Variant = TextFieldVariant.Outlined,
                Error = password.Error ?? Error,
                OnSubmit = Submit,
            },
            new Box
            {
                Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, JustifyContent = Justify.SpaceBetween },
                Children =
                [
                    new Checkbox(remember.Value, remember.Set) { TestId = RememberMe, Label = "Remember me" },
                    new SurfaceButton("Forgot password?", ButtonVariant.Text) { TestId = ForgotPassword },
                ],
            },
            new SurfaceButton("Sign in") { TestId = SignIn, OnPress = Submit, Layout = new LayoutStyle { AlignSelf = Align.Stretch } })
        {
            Variant = CardVariant.Outlined,
            Layout = new LayoutStyle { MaxWidth = 420, Padding = Edges.All(28), RowGap = 16, AlignSelf = Align.Center },
        };
    }

    // Edits made to the dots apply to the real text at the same places.
    private static TextEditState Unmask(TextEditState real, TextEditState edited)
    {
        var prefix = 0;
        while (prefix < real.Text.Length && prefix < edited.Text.Length && edited.Text[prefix] == '•')
        {
            prefix++;
        }
        var suffix = 0;
        while (suffix < real.Text.Length - prefix && suffix < edited.Text.Length - prefix && edited.Text[^(suffix + 1)] == '•')
        {
            suffix++;
        }
        var inserted = edited.Text[prefix..(edited.Text.Length - suffix)];
        var text = real.Text[..prefix] + inserted + real.Text[(real.Text.Length - suffix)..];
        return edited with { Text = text };
    }
}
