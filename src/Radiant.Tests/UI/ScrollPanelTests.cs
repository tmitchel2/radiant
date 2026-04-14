using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Input;
using Radiant.UI;

namespace Radiant.Tests.UI;

[TestClass]
public class ScrollPanelTests
{
    [TestMethod]
    public void WheelScrollShiftsChildPositionsByDelta()
    {
        var panel = new ScrollPanel
        {
            Position = new Vector2(0, 0),
            Size = new Vector2(200, 100),
            WheelStep = 10f,
        };
        var label = panel.Add(new Label("row", new Vector2(5, 50)));
        panel.ContentHeight = 400f;

        var input = new InputState { MousePosition = new Vector2(10, 50), ScrollDelta = new Vector2(0, -1) };
        panel.Update(input, 0.016);

        // Scroll offset = +10; children shift up by 10.
        Assert.AreEqual(40f, label.Position.Y, 0.01f);
    }

    [TestMethod]
    public void ScrollIsClampedToContentExtent()
    {
        var panel = new ScrollPanel
        {
            Position = new Vector2(0, 0),
            Size = new Vector2(200, 100),
            WheelStep = 500f, // one tick exceeds content
        };
        var label = panel.Add(new Label("row", new Vector2(5, 50)));
        panel.ContentHeight = 150f;

        var input = new InputState { MousePosition = new Vector2(10, 50), ScrollDelta = new Vector2(0, -1) };
        panel.Update(input, 0.016);

        // Max scroll = ContentHeight - Size.Y = 50. Child shifts up by 50 at most.
        Assert.AreEqual(0f, label.Position.Y, 0.01f);
    }

    [TestMethod]
    public void ResetScrollRestoresInitialChildPositions()
    {
        var panel = new ScrollPanel
        {
            Position = new Vector2(0, 0),
            Size = new Vector2(200, 100),
            WheelStep = 10f,
        };
        var label = panel.Add(new Label("row", new Vector2(5, 80)));
        panel.ContentHeight = 400f;

        var input = new InputState { MousePosition = new Vector2(10, 50), ScrollDelta = new Vector2(0, -1) };
        panel.Update(input, 0.016);
        Assert.AreEqual(70f, label.Position.Y, 0.01f);

        panel.ResetScroll();
        Assert.AreEqual(80f, label.Position.Y, 0.01f);
    }

    [TestMethod]
    public void WheelWhenMouseOutsideDoesNotScroll()
    {
        var panel = new ScrollPanel
        {
            Position = new Vector2(0, 0),
            Size = new Vector2(200, 100),
            WheelStep = 10f,
        };
        var label = panel.Add(new Label("row", new Vector2(5, 50)));
        panel.ContentHeight = 400f;

        var input = new InputState { MousePosition = new Vector2(500, 500), ScrollDelta = new Vector2(0, -1) };
        panel.Update(input, 0.016);

        Assert.AreEqual(50f, label.Position.Y, 0.01f);
    }
}
