using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Components;
using Radiant.UI.Core;
using static Radiant.Templates.Tests.TemplateHarness;

namespace Radiant.Templates.Tests;

[TestClass]
public class MoreTemplateTests
{
    internal static readonly DeliveryOption[] Delivery =
    [
        new("Standard", "4–10 business days", "$5.00"),
        new("Express", "2–5 business days", "$16.00"),
    ];

    internal static readonly string[] Countries = ["United Kingdom", "Ireland", "France"];

    [TestMethod]
    public void ANewsletterChecksTheAddressBeforeSubscribing()
    {
        var subscribed = new List<string>();
        using var root = Mount(new Newsletter("Stay up to date", subscribed.Add));

        Click(root, Find(root, SemanticsRole.Button, "Subscribe"));
        Assert.AreEqual(0, subscribed.Count);
        Assert.IsTrue(All(root).Any(n => n.Label == "Enter your email" || n.Semantics.Description == "Enter your email"));

        Type(root, Find(root, SemanticsRole.TextField, "Email address"), "ada@example.com");
        Click(root, Find(root, SemanticsRole.Button, "Subscribe"));

        CollectionAssert.AreEqual(new[] { "ada@example.com" }, subscribed);
        Assert.IsTrue(Shows(root, "Thanks! Check your inbox to confirm."));
    }

    [TestMethod]
    public void CheckoutPlacesTheOrderOnlyWhenEveryFieldIsFilled()
    {
        var placed = new List<CheckoutDetails>();
        using var root = Mount(new CheckoutForm(Countries, Delivery, placed.Add));

        Click(root, Find(root, SemanticsRole.Button, "Place order"));
        Assert.AreEqual(0, placed.Count, "nothing is filled in");

        Type(root, Find(root, SemanticsRole.TextField, "Email address"), "ada@example.com");
        Type(root, Find(root, SemanticsRole.TextField, "Full name"), "Ada Lovelace");
        Type(root, Find(root, SemanticsRole.TextField, "Street address"), "12 St James's Square");
        Type(root, Find(root, SemanticsRole.TextField, "City"), "London");
        Type(root, Find(root, SemanticsRole.TextField, "Postcode"), "SW1Y 4JH");
        Click(root, All(root).First(n => n.Role == SemanticsRole.RadioButton && n.Label!.StartsWith("Express", System.StringComparison.Ordinal)));
        Click(root, Find(root, SemanticsRole.Button, "Place order"));

        Assert.AreEqual(new CheckoutDetails("ada@example.com", "Ada Lovelace", "12 St James's Square", "London", "SW1Y 4JH", "United Kingdom", 1), placed.Single());
    }

    [TestMethod]
    public void ReviewsSummariseTheirRatings()
    {
        using var root = Mount(new Reviews(
        [
            new Review("Ada", 5, "Perfect", "Exactly as described."),
            new Review("Grace", 4, "Good", "Arrived quickly."),
            new Review("Alan", 3, "Fine", "Does the job."),
        ]));

        Assert.IsTrue(Shows(root, "4.0"));
        Assert.IsTrue(Shows(root, "Based on 3 reviews"));
        Assert.IsTrue(All(root).Any(n => n.Label == "4 out of 5 stars"));
        Assert.AreEqual(3, All(root).Count(n => n.Role == SemanticsRole.ListItem));
    }

    [TestMethod]
    public void TheFeedCountsTheUnreadAndMarksThemRead()
    {
        var entries = new Signal<IReadOnlyList<NotificationEntry>>(
        [
            new NotificationEntry("chat", "Ada commented on your design", "5 min ago") { Unread = true },
            new NotificationEntry("person_add", "Grace joined your team", "1 hour ago") { Unread = true },
            new NotificationEntry("check_circle", "Your export finished", "Yesterday"),
        ]);
        using var root = Mount(new Host(context => new NotificationFeed(context.Watch(entries))
        {
            OnMarkAllRead = () => entries.Value = [.. entries.Value.Select(e => e with { Unread = false })],
        }));
        Assert.IsTrue(Shows(root, "2 new"));
        Assert.AreEqual(2, All(root).Count(n => n.Label == "Unread"));

        Click(root, Find(root, SemanticsRole.Button, "Mark all as read"));

        Assert.IsFalse(Shows(root, "2 new"));
        Assert.IsFalse(All(root).Any(n => n.Label == "Unread"));
        Assert.IsNull(TryFind(root, SemanticsRole.Button, "Mark all as read"));
    }

    [TestMethod]
    public void AFootersLinksReportWhichWasFollowed()
    {
        var followed = new List<string>();
        using var root = Mount(new SiteFooter("Radiant", [new FooterColumn("Product", ["Features", "Pricing"]) { OnFollow = followed.Add }]) { Copyright = "© 2026 Radiant" });

        Click(root, Find(root, SemanticsRole.Link, "Pricing"));

        CollectionAssert.AreEqual(new[] { "Pricing" }, followed);
        Assert.IsTrue(Shows(root, "© 2026 Radiant"));
    }
}
