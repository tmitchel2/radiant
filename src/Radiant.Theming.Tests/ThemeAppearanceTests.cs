using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;
using Radiant.Platform;
using Radiant.UI.Core;

namespace Radiant.Theming.Tests;

[TestClass]
public class ThemeAppearanceTests
{
    private static HeadlessAppearance Dark() => new()
    {
        IsDark = true,
        AccentColor = 0xFFFF9500,
        IncreaseContrast = true,
        ReduceMotion = true,
    };

    [TestMethod]
    public void EachSettingMapsToItsPartOfTheTheme()
    {
        var theme = new Theme().WithAppearance(Dark());

        Assert.IsTrue(theme.Colors.IsDark);
        Assert.AreEqual(Color.FromArgb(0xFFFF9500), theme.Colors.Seed);
        Assert.AreEqual(1.0, theme.Colors.ContrastLevel);
        Assert.IsTrue(theme.Motion.Reduced);
    }

    [TestMethod]
    public void WithoutIncreasedContrastTheStandardLevelIsUsed()
    {
        var appearance = new HeadlessAppearance();
        var start = new Theme { Colors = new ThemeColors { ContrastLevel = 1 } };

        Assert.AreEqual(0.5, start.WithAppearance(appearance, standardContrast: 0.5).Colors.ContrastLevel);
    }

    [TestMethod]
    public void SettingsNotFollowedAreLeftAlone()
    {
        var start = new Theme();

        var theme = start.WithAppearance(Dark(), new AppearanceFollowing { Accent = false, Motion = false });

        Assert.AreEqual(start.Colors.Seed, theme.Colors.Seed);
        Assert.IsFalse(theme.Motion.Reduced);
        Assert.IsTrue(theme.Colors.IsDark);
    }

    [TestMethod]
    public void FollowingAppliesAtOnceThenAnimatesChanges()
    {
        var appearance = new HeadlessAppearance();
        var controller = new ThemeController(new Theme { Colors = new ThemeColors { IsDark = true } });

        using var following = controller.FollowAppearance(appearance);
        // The system is light, so the theme is at once.
        Assert.IsFalse(controller.Theme.Colors.IsDark);
        Assert.IsFalse(controller.IsAnimating);

        appearance.IsDark = true;
        Assert.IsTrue(controller.Theme.Colors.IsDark);
        Assert.IsTrue(controller.IsAnimating, "the user's change animates");
    }

    [TestMethod]
    public void WithReducedMotionChangesAreImmediate()
    {
        var appearance = new HeadlessAppearance { ReduceMotion = true };
        var controller = new ThemeController();
        using var following = controller.FollowAppearance(appearance);

        appearance.IsDark = true;

        Assert.IsFalse(controller.IsAnimating);
        Assert.IsTrue(controller.Current.Value.Theme.Colors.IsDark);
    }

    [TestMethod]
    public void TurningIncreasedContrastOffRestoresTheThemesLevel()
    {
        var appearance = new HeadlessAppearance();
        var controller = new ThemeController(new Theme { Colors = new ThemeColors { ContrastLevel = 0.5 } });
        using var following = controller.FollowAppearance(appearance);

        appearance.IncreaseContrast = true;
        Assert.AreEqual(1.0, controller.Theme.Colors.ContrastLevel);
        appearance.IncreaseContrast = false;
        Assert.AreEqual(0.5, controller.Theme.Colors.ContrastLevel);
    }

    [TestMethod]
    public void DisposingStopsFollowing()
    {
        var appearance = new HeadlessAppearance();
        var controller = new ThemeController();
        var following = controller.FollowAppearance(appearance);

        following.Dispose();
        appearance.IsDark = true;

        Assert.IsFalse(controller.Theme.Colors.IsDark);
    }

    [TestMethod]
    public void AProviderFollowsThePlatformsAppearanceBeforeTheFirstFrame()
    {
        var platform = new HeadlessPlatform();
        platform.Appearance.IsDark = true;
        var controller = new ThemeController();
        using var root = new UIRoot(PlatformContext.Platform.Provide(platform,
            new ThemeProvider(controller, null) { FollowAppearance = true }));

        root.Update(new Vector2(100, 100));
        Assert.IsTrue(controller.Current.Value.Theme.Colors.IsDark);

        // Unmounting stops following.
        root.Dispose();
        platform.Appearance.IsDark = false;
        Assert.IsTrue(controller.Theme.Colors.IsDark);
    }

    [TestMethod]
    public void AProviderDoesNotFollowUnlessAsked()
    {
        var platform = new HeadlessPlatform();
        platform.Appearance.IsDark = true;
        var controller = new ThemeController();
        using var root = new UIRoot(PlatformContext.Platform.Provide(platform, new ThemeProvider(controller, null)));

        root.Update(new Vector2(100, 100));

        Assert.IsFalse(controller.Theme.Colors.IsDark);
    }
}
