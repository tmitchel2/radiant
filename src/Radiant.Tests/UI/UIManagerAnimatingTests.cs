using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;
using Radiant.Input;
using Radiant.Scrolling;
using Radiant.UI;

namespace Radiant.Tests.UI;

[TestClass]
public class UIManagerAnimatingTests
{
    private sealed class Spacer : UIElement
    {
        public override void Draw(Renderer2D renderer) { }
    }

    [TestMethod]
    public void NeedsContinuousFrameTracksScrollMomentum()
    {
        var ui = new UIManager();
        var sv = new ScrollView(new ScrollBehaviour { Overscroll = OverscrollMode.Clamp })
        {
            Position = Vector2.Zero,
            Size = new Vector2(100, 100),
        };
        sv.Add(new Spacer { Position = Vector2.Zero, Size = new Vector2(100, 1000) });
        ui.Add(sv);

        Assert.IsFalse(ui.NeedsContinuousFrame, "idle at rest");

        // Fling: a programmatic animated scroll starts time-driven motion.
        sv.ScrollTo(new Vector2(0, 400), animated: true);
        Assert.IsTrue(ui.NeedsContinuousFrame, "animating scroll needs continuous frames");

        for (var i = 0; i < 2000 && sv.IsAnimating; i++)
            sv.Update(new InputState { MousePosition = new Vector2(-1, -1) }, 1.0 / 60.0);

        Assert.IsFalse(ui.NeedsContinuousFrame, "back to idle once settled");
    }

    [TestMethod]
    public void NeedsContinuousFrameFindsNestedScrollers()
    {
        var ui = new UIManager();
        var panel = new Panel(TestFonts.Default) { Position = Vector2.Zero, Size = new Vector2(200, 200) };
        var sv = new ScrollView(new ScrollBehaviour { Overscroll = OverscrollMode.Clamp })
        {
            Position = Vector2.Zero,
            Size = new Vector2(100, 100),
        };
        sv.Add(new Spacer { Position = Vector2.Zero, Size = new Vector2(100, 1000) });
        panel.Add(sv);
        ui.Add(panel);

        sv.ScrollTo(new Vector2(0, 400), animated: true);
        Assert.IsTrue(ui.NeedsContinuousFrame, "must walk into nested containers");
    }
}
