using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.Platform.MacOS.Tests;

[TestClass]
public class CursorTests
{
    [TestMethod]
    public void EveryShapeIsASystemCursor()
    {
        MacOnly.Require();
        var cursors = new MacCursorService();
        var arrow = cursors.Cursor(CursorShape.Arrow);
        Assert.AreNotEqual(0, arrow);

        var seen = new HashSet<nint>();
        foreach (var shape in Enum.GetValues<CursorShape>())
        {
            var cursor = cursors.Cursor(shape);
            Assert.AreNotEqual(0, cursor, shape.ToString());
            Assert.IsTrue(ObjC.IsKindOf(cursor, "NSCursor"), shape.ToString());
            // Each shape is its own cursor: none fell back to the arrow.
            Assert.IsTrue(seen.Add(cursor), $"{shape} is the same cursor as another shape");
        }
    }

    [TestMethod]
    public void ShowingAShapeMakesItTheCurrentCursor()
    {
        MacOnly.Require();
        // Harmless to the user: a process with no window isn't the one the window server takes
        // the cursor from.
        var cursors = new MacCursorService();
        foreach (var shape in new[] { CursorShape.IBeam, CursorShape.PointingHand, CursorShape.Arrow })
        {
            cursors.Show(shape);

            Assert.AreEqual(shape, cursors.Current);
            using var pool = ObjC.Pool();
            Assert.AreEqual(cursors.Cursor(shape), ObjC.Send(ObjC.Class("NSCursor"), "currentCursor"), shape.ToString());
        }
    }

    [TestMethod]
    public void AShapeOutOfRangeIsTheArrow()
    {
        MacOnly.Require();
        var cursors = new MacCursorService();

        Assert.AreEqual(cursors.Cursor(CursorShape.Arrow), cursors.Cursor((CursorShape)999));
    }
}
