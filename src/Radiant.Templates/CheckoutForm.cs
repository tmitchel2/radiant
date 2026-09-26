using System;
using System.Collections.Generic;
using System.Linq;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>
/// A checkout's details: contact email, shipping address (the city and postcode side by side where
/// there's room) and a delivery option, each group under a legend. Placing the order checks every
/// field first, showing what's missing, and calls <paramref name="OnPlaceOrder"/> once all are right.
/// </summary>
/// <param name="Countries">The countries it ships to.</param>
/// <param name="Delivery">The delivery options.</param>
/// <param name="OnPlaceOrder">Called with the details.</param>
public sealed partial record CheckoutForm(IReadOnlyList<string> Countries, IReadOnlyList<DeliveryOption> Delivery, Action<CheckoutDetails> OnPlaceOrder) : Component
{
    [TestId<TextField>] public static partial string Email { get; }
    [TestId<TextField>] public static partial string FullName { get; }
    [TestId<TextField>] public static partial string Street { get; }
    [TestId<TextField>] public static partial string City { get; }
    [TestId<TextField>] public static partial string Postcode { get; }
    [TestId<SelectField>] public static partial string Country { get; }
    [TestId<Radio>] public static partial string DeliveryOption { get; }
    [TestId<SurfaceButton>] public static partial string PlaceOrder { get; }

    /// <summary>The button's label, with the total ("Pay $142.00").</summary>
    public string PlaceOrderLabel { get; init; } = "Place order";

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var form = context.UseForm();
        var email = form.Field("email", "", Validators.Required("Enter your email"), Validators.Email());
        var name = form.Field("name", "", Validators.Required("Enter the recipient's name"));
        var address = form.Field("address", "", Validators.Required("Enter the street address"));
        var city = form.Field("city", "", Validators.Required("Enter the town or city"));
        var postcode = form.Field("postcode", "", Validators.Required("Enter the postcode"));
        var country = context.UseState(0);
        var delivery = context.UseState(0);
        var (countries, place) = (Countries, OnPlaceOrder);

        void Submit() => form.Submit(values => place(new CheckoutDetails(
            values["email"], values["name"], values["address"], values["city"], values["postcode"],
            countries.Count > 0 ? countries[country.Value] : "", delivery.Value)));

        // A field fills its line; two sharing a line (inRow) split it, wrapping when it's narrow.
        Element Field(string testId, FormField field, string label, string? icon = null, bool inRow = false) => new TextField(label)
        {
            TestId = testId,
            Value = field.State,
            OnChange = field.Set,
            OnFocusChange = field.FocusChanged,
            InputRef = field.InputRef,
            Error = field.Error,
            LeadingIcon = icon,
            Variant = TextFieldVariant.Outlined,
            Layout = inRow ? new LayoutStyle { FlexGrow = 1, FlexShrink = 1, FlexBasis = 160 } : new LayoutStyle { AlignSelf = Align.Stretch },
        };

        return new Box
        {
            Layout = new LayoutStyle { RowGap = 20, AlignSelf = Align.Stretch, MaxWidth = 560 },
            Children =
            [
                new Fieldset("Contact", [Field(Email, email, "Email address", "mail")]),
                new Fieldset("Shipping address",
                [
                    Field(FullName, name, "Full name"),
                    Field(Street, address, "Street address"),
                    new Box
                    {
                        Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, FlexWrap = FlexWrap.Wrap, ColumnGap = 12, RowGap = 12 },
                        Children = [Field(City, city, "City", inRow: true), Field(Postcode, postcode, "Postcode", inRow: true)],
                    },
                    new SelectField("Country", Countries, country.Value, country.Set) { TestId = Country, Layout = new LayoutStyle { AlignSelf = Align.Stretch } },
                ]),
                new Fieldset("Delivery", [.. Delivery.Select((option, i) => (Element?)new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 8 },
                    Children =
                    [
                        new Radio(delivery.Value == i, () => delivery.Set(i)) { TestId = DeliveryOption, AccessibleLabel = $"{option.Name}, {option.Detail}, {option.Price}" },
                        new Box
                        {
                            Layout = new LayoutStyle { FlexGrow = 1 },
                            Children =
                            [
                                new SurfaceText(option.Name) { TextType = TextType.TitleSmall },
                                new SurfaceText(option.Detail) { TextType = TextType.BodySmall, Legibility = Legibility.Medium },
                            ],
                        },
                        new SurfaceText(option.Price) { TextType = TextType.TitleSmall },
                    ],
                })]),
                new SurfaceButton(PlaceOrderLabel) { TestId = PlaceOrder, Icon = "lock", OnPress = Submit, Layout = new LayoutStyle { AlignSelf = Align.Stretch } },
            ],
        };
    }
}
