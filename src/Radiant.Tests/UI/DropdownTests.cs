using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Input;
using Radiant.UI;
using Silk.NET.Input;

namespace Radiant.Tests.UI;

[TestClass]
public class DropdownTests
{
    private static Dropdown CreateDropdown() => new(
        ["Alpha", "Beta", "Gamma"],
        0,
        new Vector2(10, 10),
        new Vector2(200, 25));

    [TestMethod]
    public void WhenClosed_IsNotCapturingInput()
    {
        var dropdown = CreateDropdown();
        Assert.IsFalse(dropdown.IsCapturingInput);
    }

    [TestMethod]
    public void ClickHeader_OpensDropdown()
    {
        var dropdown = CreateDropdown();
        var input = new InputState();
        input.MousePosition = new Vector2(100, 20); // Inside header
        input.SetMouseButton(MouseButton.Left, true);

        dropdown.Update(input, 0.016);

        Assert.IsTrue(dropdown.IsCapturingInput);
    }

    [TestMethod]
    public void WhenOpen_ClickItem_SelectsAndCloses()
    {
        var dropdown = CreateDropdown();
        var selectedIndex = -1;
        dropdown.SelectionChanged += idx => selectedIndex = idx;

        // Open
        var input = new InputState();
        input.MousePosition = new Vector2(100, 20);
        input.SetMouseButton(MouseButton.Left, true);
        dropdown.Update(input, 0.016);
        Assert.IsTrue(dropdown.IsCapturingInput, "Precondition: dropdown should be open");

        // Release mouse, end frame
        input.EndFrame();
        input.SetMouseButton(MouseButton.Left, false);
        dropdown.Update(input, 0.016);
        input.EndFrame();

        // Click second item: listTop = 10+25 = 35, item 1 at y = 35+25 = 60..85, center = 72
        input.MousePosition = new Vector2(100, 72);
        input.SetMouseButton(MouseButton.Left, true);
        dropdown.Update(input, 0.016);

        Assert.IsFalse(dropdown.IsCapturingInput, "Should close after selection");
        Assert.AreEqual(1, dropdown.SelectedIndex);
        Assert.AreEqual("Beta", dropdown.SelectedText);
        Assert.AreEqual(1, selectedIndex);
    }

    [TestMethod]
    public void WhenOpen_ClickOutside_ClosesWithoutSelectionChange()
    {
        var dropdown = CreateDropdown();
        var selectionChanged = false;
        dropdown.SelectionChanged += _ => selectionChanged = true;

        // Open
        var input = new InputState();
        input.MousePosition = new Vector2(100, 20);
        input.SetMouseButton(MouseButton.Left, true);
        dropdown.Update(input, 0.016);
        Assert.IsTrue(dropdown.IsCapturingInput);

        // Release mouse, end frame
        input.EndFrame();
        input.SetMouseButton(MouseButton.Left, false);
        dropdown.Update(input, 0.016);
        input.EndFrame();

        // Click outside (far below list: 10 + 25 + 3*25 = 110, click at y=200)
        input.MousePosition = new Vector2(300, 200);
        input.SetMouseButton(MouseButton.Left, true);
        dropdown.Update(input, 0.016);

        Assert.IsFalse(dropdown.IsCapturingInput, "Should close after clicking outside");
        Assert.AreEqual(0, dropdown.SelectedIndex, "Selection should not change");
        Assert.IsFalse(selectionChanged, "SelectionChanged should not fire");
    }

    [TestMethod]
    public void PanelInputGating_OpenDropdownBlocksSiblingButton()
    {
        var dropdown = CreateDropdown();
        var button = new Radiant.UI.Button("Test", new Vector2(10, 50), new Vector2(200, 25));
        var buttonClicked = false;
        button.Clicked += () => buttonClicked = true;

        var panel = new Panel(new Vector2(0, 0), new Vector2(300, 200));
        panel.Add(dropdown);
        panel.Add(button);

        // Open dropdown
        var input = new InputState();
        input.MousePosition = new Vector2(100, 20);
        input.SetMouseButton(MouseButton.Left, true);
        panel.Update(input, 0.016);
        Assert.IsTrue(dropdown.IsCapturingInput, "Precondition: dropdown should be open");

        // Release mouse, end frame
        input.EndFrame();
        input.SetMouseButton(MouseButton.Left, false);
        panel.Update(input, 0.016);
        input.EndFrame();

        // Click in list area (which overlaps button position)
        input.MousePosition = new Vector2(100, 55);
        input.SetMouseButton(MouseButton.Left, true);
        panel.Update(input, 0.016);

        Assert.IsFalse(buttonClicked, "Sibling button should not fire when dropdown is open");
    }

    [TestMethod]
    public void SelectedText_ReturnsCorrectItem()
    {
        var dropdown = CreateDropdown();
        Assert.AreEqual("Alpha", dropdown.SelectedText);

        dropdown.SelectedIndex = 2;
        Assert.AreEqual("Gamma", dropdown.SelectedText);
    }

    [TestMethod]
    public void SelectedIndex_ClampedToValidRange()
    {
        var dropdown = CreateDropdown();

        dropdown.SelectedIndex = -5;
        Assert.AreEqual(0, dropdown.SelectedIndex);

        dropdown.SelectedIndex = 100;
        Assert.AreEqual(2, dropdown.SelectedIndex);
    }
}
