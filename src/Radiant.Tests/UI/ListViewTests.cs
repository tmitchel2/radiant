using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Input;
using Radiant.UI;
using Silk.NET.Input;

namespace Radiant.Tests.UI;

[TestClass]
public class ListViewTests
{
    private static ListView Make()
    {
        return new ListView(TestFonts.Default)
        {
            Position = Vector2.Zero,
            Size = new Vector2(200, 100), // VisibleHeight = 100 - 2*8 = 84
            RowHeight = 28f,
            Padding = 8f,
            Items = new[] { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j" }, // content = 280
        };
    }

    [TestMethod]
    public void WheelScrollsByRowHeightAndClamps()
    {
        var lv = Make();
        var input = new InputState { MousePosition = new Vector2(50, 50), ScrollDelta = new Vector2(0, -1) };
        lv.Update(input, 1.0 / 60.0);
        Assert.AreEqual(28f, lv.ScrollOffset, 0.01f, "one notch = one RowHeight (matching legacy behaviour)");

        // Hammer the wheel; must clamp at content - visible = 280 - 84 = 196.
        for (var i = 0; i < 50; i++)
            lv.Update(new InputState { MousePosition = new Vector2(50, 50), ScrollDelta = new Vector2(0, -1) }, 1.0 / 60.0);
        Assert.AreEqual(196f, lv.ScrollOffset, 0.01f);
    }

    [TestMethod]
    public void WheelOutsideBoundsDoesNotScroll()
    {
        var lv = Make();
        lv.Update(new InputState { MousePosition = new Vector2(500, 500), ScrollDelta = new Vector2(0, -1) }, 1.0 / 60.0);
        Assert.AreEqual(0f, lv.ScrollOffset, 0.01f);
    }

    [TestMethod]
    public void CapturesInputWhenScrollable()
    {
        var lv = Make();
        lv.Update(new InputState { MousePosition = new Vector2(50, 50) }, 1.0 / 60.0);
        Assert.IsTrue(lv.IsCapturingInput);
    }

    [TestMethod]
    public void SelectionFiresOnClickAfterScroll()
    {
        var lv = Make();
        var selected = -1;
        lv.SelectionChanged += i => selected = i;

        // Scroll down one row, then click the first visible row.
        lv.Update(new InputState { MousePosition = new Vector2(50, 50), ScrollDelta = new Vector2(0, -1) }, 1.0 / 60.0);

        var click = new InputState { MousePosition = new Vector2(50, 8 + 1) }; // top of content area
        click.SetMouseButton(MouseButton.Left, true);
        lv.Update(click, 1.0 / 60.0);

        // contentTop = 8; relY = 9 - 8 + 28 = 29 → row 1.
        Assert.AreEqual(1, selected);
        Assert.AreEqual(1, lv.SelectedIndex);
    }
}
