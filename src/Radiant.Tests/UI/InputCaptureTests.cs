using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Input;
using Radiant.UI;
using Silk.NET.Input;

namespace Radiant.Tests.UI;

[TestClass]
public class InputCaptureTests
{
    [TestMethod]
    public void Slider_WhenNotDragging_IsNotCapturingInput()
    {
        // Arrange
        var slider = new Slider
        {
            Position = new Vector2(10, 50),
            Size = new Vector2(200, 24),
            MinValue = 0,
            MaxValue = 100,
            Value = 50,
            Label = "",
            ShowValue = true
        };

        // Act - no interaction

        // Assert
        Assert.IsFalse(slider.IsCapturingInput, "Slider should not capture input when not dragging");
    }

    [TestMethod]
    public void Slider_WhenDragging_IsCapturingInput()
    {
        // Arrange
        var slider = new Slider
        {
            Position = new Vector2(10, 50),
            Size = new Vector2(200, 24),
            MinValue = 0,
            MaxValue = 100,
            Value = 50,
            Label = "",
            ShowValue = true
        };

        // Calculate handle position:
        // trackX = Position.X + labelWidth = 10 + 0 = 10
        // valueWidth = ShowValue ? 50 : 0 = 50
        // trackWidth = Size.X - labelWidth - valueWidth - HandleRadius = 200 - 0 - 50 - 8 = 142
        // handleX = trackX + NormalizedValue * trackWidth = 10 + 0.5 * 142 = 81
        // handleY = Position.Y + Size.Y / 2 = 50 + 12 = 62

        var inputState = new InputState();
        inputState.MousePosition = new Vector2(81, 62); // Over handle
        inputState.SetMouseButton(MouseButton.Left, true);

        // Act
        slider.Update(inputState, 0.016);

        // Assert
        Assert.IsTrue(slider.IsCapturingInput, "Slider should capture input when dragging");
    }

    [TestMethod]
    public void Slider_WhenClickOnTrack_IsCapturingInput()
    {
        // Arrange
        var slider = new Slider
        {
            Position = new Vector2(10, 50),
            Size = new Vector2(200, 24),
            MinValue = 0,
            MaxValue = 100,
            Value = 50,
            Label = "",
            ShowValue = true
        };

        // Click on the track area (within slider bounds but not exactly on handle)
        var inputState = new InputState();
        inputState.MousePosition = new Vector2(50, 62); // On track, not on handle
        inputState.SetMouseButton(MouseButton.Left, true);

        // Act
        slider.Update(inputState, 0.016);

        // Assert
        Assert.IsTrue(slider.IsCapturingInput, "Slider should capture input when clicking on track");
    }

    [TestMethod]
    public void Slider_WhenReleased_IsNotCapturingInput()
    {
        // Arrange
        var slider = new Slider
        {
            Position = new Vector2(10, 50),
            Size = new Vector2(200, 24),
            MinValue = 0,
            MaxValue = 100,
            Value = 50,
            Label = "",
            ShowValue = true
        };

        var inputState = new InputState();
        inputState.MousePosition = new Vector2(81, 62);
        inputState.SetMouseButton(MouseButton.Left, true);

        // Start dragging
        slider.Update(inputState, 0.016);
        Assert.IsTrue(slider.IsCapturingInput, "Precondition: slider should be dragging");

        // Release mouse button
        inputState.EndFrame();
        inputState.SetMouseButton(MouseButton.Left, false);

        // Act
        slider.Update(inputState, 0.016);

        // Assert
        Assert.IsFalse(slider.IsCapturingInput, "Slider should not capture input after releasing");
    }

    [TestMethod]
    public void Panel_WhenChildCapturing_IsCapturingInput()
    {
        // Arrange
        var slider = new Slider
        {
            Position = new Vector2(20, 60),
            Size = new Vector2(200, 24),
            MinValue = 0,
            MaxValue = 100,
            Value = 50,
            Label = "",
            ShowValue = true
        };

        var panel = new Panel(new Vector2(10, 50), new Vector2(220, 100));
        panel.Add(slider);

        // Panel should not be capturing initially
        Assert.IsFalse(panel.IsCapturingInput, "Precondition: panel should not capture when idle");

        // Calculate handle position for this slider:
        // trackX = 20, trackWidth = 200 - 0 - 50 - 8 = 142, handleX = 20 + 0.5 * 142 = 91
        // handleY = 60 + 12 = 72

        var inputState = new InputState();
        inputState.MousePosition = new Vector2(91, 72);
        inputState.SetMouseButton(MouseButton.Left, true);

        // Act
        panel.Update(inputState, 0.016);

        // Assert
        Assert.IsTrue(slider.IsCapturingInput, "Child slider should capture input when dragging");
        Assert.IsTrue(panel.IsCapturingInput, "Panel should capture input when child is capturing");
    }

    [TestMethod]
    public void Panel_WhenNoChildCapturing_IsNotCapturingInput()
    {
        // Arrange
        var slider = new Slider
        {
            Position = new Vector2(20, 60),
            Size = new Vector2(200, 24),
            MinValue = 0,
            MaxValue = 100,
            Value = 50,
            Label = "",
            ShowValue = true
        };

        var panel = new Panel(new Vector2(10, 50), new Vector2(220, 100));
        panel.Add(slider);

        // Act - no interaction

        // Assert
        Assert.IsFalse(panel.IsCapturingInput, "Panel should not capture input when no child is capturing");
    }

    [TestMethod]
    public void UIManager_WhenElementCapturing_IsCapturingInput()
    {
        // Arrange
        var slider = new Slider
        {
            Position = new Vector2(20, 60),
            Size = new Vector2(200, 24),
            MinValue = 0,
            MaxValue = 100,
            Value = 50,
            Label = "",
            ShowValue = true
        };

        var panel = new Panel(new Vector2(10, 50), new Vector2(220, 100));
        panel.Add(slider);

        var uiManager = new UIManager();
        uiManager.Add(panel);

        // UIManager should not be capturing initially
        Assert.IsFalse(uiManager.IsCapturingInput, "Precondition: UIManager should not capture when idle");

        // Calculate handle position for this slider
        var inputState = new InputState();
        inputState.MousePosition = new Vector2(91, 72);
        inputState.SetMouseButton(MouseButton.Left, true);

        // Act
        uiManager.Update(inputState, 0.016);

        // Assert
        Assert.IsTrue(slider.IsCapturingInput, "Slider should capture input when dragging");
        Assert.IsTrue(panel.IsCapturingInput, "Panel should capture input when child is capturing");
        Assert.IsTrue(uiManager.IsCapturingInput, "UIManager should capture input when element is capturing");
    }

    [TestMethod]
    public void UIManager_WhenNoElementCapturing_IsNotCapturingInput()
    {
        // Arrange
        var slider = new Slider
        {
            Position = new Vector2(20, 60),
            Size = new Vector2(200, 24),
            MinValue = 0,
            MaxValue = 100,
            Value = 50,
            Label = "",
            ShowValue = true
        };

        var panel = new Panel(new Vector2(10, 50), new Vector2(220, 100));
        panel.Add(slider);

        var uiManager = new UIManager();
        uiManager.Add(panel);

        // Act - no interaction

        // Assert
        Assert.IsFalse(uiManager.IsCapturingInput, "UIManager should not capture input when no element is capturing");
    }

    [TestMethod]
    public void SimulateInteractiveDemoFlow()
    {
        // This test replicates the exact flow in InteractiveDemo.OnUpdate:
        // 1. uiManager.Update(inputState, delta)
        // 2. Check uiManager.IsCapturingInput

        // Arrange
        var slider = new Slider
        {
            Position = new Vector2(20, 60),
            Size = new Vector2(200, 24),
            MinValue = 0,
            MaxValue = 100,
            Value = 50,
            Label = "",
            ShowValue = true
        };

        var panel = new Panel(new Vector2(10, 50), new Vector2(220, 100));
        panel.Add(slider);

        var uiManager = new UIManager();
        uiManager.Add(panel);

        var inputState = new InputState();

        // Frame 1: Mouse moves over slider handle
        inputState.MousePosition = new Vector2(91, 72);
        uiManager.Update(inputState, 0.016);
        Assert.IsFalse(uiManager.IsCapturingInput, "Frame 1: No capture without click");

        inputState.EndFrame();

        // Frame 2: Mouse button pressed
        inputState.SetMouseButton(MouseButton.Left, true);
        uiManager.Update(inputState, 0.016);
        Assert.IsTrue(uiManager.IsCapturingInput, "Frame 2: Should capture on mouse press");

        inputState.EndFrame();

        // Frame 3: Dragging continues (mouse held, moved)
        inputState.MousePosition = new Vector2(120, 72);
        uiManager.Update(inputState, 0.016);
        Assert.IsTrue(uiManager.IsCapturingInput, "Frame 3: Should still capture while dragging");

        inputState.EndFrame();

        // Frame 4: Mouse released
        inputState.SetMouseButton(MouseButton.Left, false);
        uiManager.Update(inputState, 0.016);
        Assert.IsFalse(uiManager.IsCapturingInput, "Frame 4: Should not capture after release");
    }

    [TestMethod]
    public void UIManager_IsCapturingInput_CheckedAfterUpdate()
    {
        // Verify the exact sequence matters: Update first, then check IsCapturingInput

        // Arrange
        var slider = new Slider
        {
            Position = new Vector2(10, 50),
            Size = new Vector2(200, 24),
            MinValue = 0,
            MaxValue = 100,
            Value = 50,
            Label = "",
            ShowValue = true
        };

        var uiManager = new UIManager();
        uiManager.Add(slider);

        var inputState = new InputState();
        inputState.MousePosition = new Vector2(81, 62);
        inputState.SetMouseButton(MouseButton.Left, true);

        // Check before update - should be false
        Assert.IsFalse(uiManager.IsCapturingInput, "Before Update: should not be capturing");

        // Act
        uiManager.Update(inputState, 0.016);

        // Check after update - should be true
        Assert.IsTrue(uiManager.IsCapturingInput, "After Update: should be capturing");
    }

    [TestMethod]
    public void Slider_WhenMousePositionNotUpdatedBeforeClick_FailsToCapture()
    {
        // This test demonstrates the bug: if mouse position isn't updated before
        // the click (e.g., first click after window opens), hover check fails
        // because position is still at default (0, 0).

        // Arrange
        var slider = new Slider
        {
            Position = new Vector2(20, 70),
            Size = new Vector2(200, 24),
            MinValue = 0,
            MaxValue = 100,
            Value = 50,
            Label = "",
            ShowValue = true
        };

        var inputState = new InputState();
        // DON'T set MousePosition - simulate the bug where position is at default (0, 0)
        // inputState.MousePosition = new Vector2(50, 80); // Intentionally omitted!
        inputState.SetMouseButton(MouseButton.Left, true);

        // Act
        slider.Update(inputState, 0.016);

        // Assert - This demonstrates the bug: slider doesn't capture because
        // hover check fails with position (0, 0)
        Assert.IsFalse(slider.IsCapturingInput,
            "Bug demonstration: slider fails to capture when position not updated before click");
    }

    [TestMethod]
    public void Slider_WhenMousePositionUpdatedWithClick_CorrectlyCaptures()
    {
        // This test shows the correct behavior when position IS updated with the click
        // (simulating the fix where MouseDown handler updates position)

        // Arrange
        var slider = new Slider
        {
            Position = new Vector2(20, 70),
            Size = new Vector2(200, 24),
            MinValue = 0,
            MaxValue = 100,
            Value = 50,
            Label = "",
            ShowValue = true
        };

        var inputState = new InputState();
        // Simulate the fix: update position AND button state together
        inputState.MousePosition = new Vector2(50, 80); // Over the slider
        inputState.SetMouseButton(MouseButton.Left, true);

        // Act
        slider.Update(inputState, 0.016);

        // Assert
        Assert.IsTrue(slider.IsCapturingInput,
            "Slider should capture when position is updated with click");
    }

    [TestMethod]
    public void Slider_DragMovesValue()
    {
        // Ensure slider drag actually modifies value (sanity check)

        // Arrange
        var slider = new Slider
        {
            Position = new Vector2(10, 50),
            Size = new Vector2(200, 24),
            MinValue = 0,
            MaxValue = 100,
            Value = 50,
            Label = "",
            ShowValue = true
        };

        var initialValue = slider.Value;

        var inputState = new InputState();
        inputState.MousePosition = new Vector2(81, 62);
        inputState.SetMouseButton(MouseButton.Left, true);

        // Start drag
        slider.Update(inputState, 0.016);
        inputState.EndFrame();

        // Drag to new position (towards end of track)
        inputState.MousePosition = new Vector2(140, 62);
        slider.Update(inputState, 0.016);

        // Assert
        Assert.AreNotEqual(initialValue, slider.Value, "Slider value should change when dragged");
        Assert.IsTrue(slider.Value > initialValue, "Slider value should increase when dragged right");
    }
}
