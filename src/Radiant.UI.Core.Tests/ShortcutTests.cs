using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;

namespace Radiant.UI.Core.Tests;

[TestClass]
public class ShortcutTests
{
    private static readonly Vector2 Viewport = new(200, 100);

    private sealed record Shortcuts(List<string> Log, KeyChord Chord, string Name, Element? Child = null) : Component
    {
        public override Element? Build(BuildContext context)
        {
            var log = Log;
            var name = Name;
            context.UseShortcut(Chord, () => log.Add(name));
            return Child;
        }
    }

    [TestMethod]
    public void AShortcutRunsWithNothingFocused()
    {
        var log = new List<string>();
        using var root = new UIRoot(new Shortcuts(log, KeyChord.Command(KeyCode.K), "palette"));
        root.Update(Viewport);

        root.KeyDown(KeyCode.K);
        root.KeyDown(KeyCode.K, KeyChord.CommandModifier | KeyModifiers.Shift);
        root.KeyDown(KeyCode.K, KeyChord.CommandModifier);

        CollectionAssert.AreEqual(new[] { "palette" }, log, "only the exact chord");
    }

    [TestMethod]
    public void WhatHasFocusGetsTheKeyFirst()
    {
        var log = new List<string>();
        using var root = new UIRoot(new Shortcuts(log, new KeyChord(KeyCode.Enter), "shortcut", new Box
        {
            Focusable = true,
            Layout = new LayoutStyle { Width = 50, Height = 50 },
            OnKeyDown = e =>
            {
                log.Add("box");
                e.Handled = true;
            },
        }));
        root.Update(Viewport);
        root.PointerDown(new Vector2(10, 10));
        root.PointerUp(new Vector2(10, 10));

        root.KeyDown(KeyCode.Enter);

        CollectionAssert.AreEqual(new[] { "box" }, log);
    }

    [TestMethod]
    public void TheLatestShortcutForAChordWinsAndGoesWithItsComponent()
    {
        var log = new List<string>();
        var inner = new Signal<bool>(true);
        var chord = KeyChord.Command(KeyCode.P);
        using var root = new UIRoot(new Host(ctx => new Shortcuts(log, chord, "outer", ctx.Watch(inner) ? new Shortcuts(log, chord, "inner") : null)));
        root.Update(Viewport);

        root.KeyDown(KeyCode.P, KeyChord.CommandModifier);
        inner.Value = false;
        root.Update(Viewport);
        root.KeyDown(KeyCode.P, KeyChord.CommandModifier);

        CollectionAssert.AreEqual(new[] { "inner", "outer" }, log);
    }

    [TestMethod]
    public void ChordsReadAsThePlatformWritesThem()
    {
        var chord = KeyChord.Command(KeyCode.P, KeyModifiers.Shift);

        Assert.AreEqual(OperatingSystem.IsMacOS() ? "⇧⌘P" : "Ctrl+Shift+P", chord.ToString());
        Assert.AreEqual("Esc", new KeyChord(KeyCode.Escape).ToString());
    }

    private sealed record Host(Func<BuildContext, Element?> Body) : Component
    {
        public override Element? Build(BuildContext context) => Body(context);
    }
}
