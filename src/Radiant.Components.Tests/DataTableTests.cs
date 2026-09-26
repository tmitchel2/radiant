using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components.Tests;

[TestClass]
public class DataTableTests
{
    private static readonly Vector2 Viewport = new(800, 400);

    private static readonly KeyModifiers Command = OperatingSystem.IsMacOS() ? KeyModifiers.Super : KeyModifiers.Control;

    private static UIRoot Mount(Element element)
    {
        var root = new UIRoot(new ThemeProvider(new ThemeController(), new Box { Layout = new LayoutStyle { FlexGrow = 1 }, Children = [element] }));
        Settle(root);
        return root;
    }

    private static void Settle(UIRoot root)
    {
        for (var i = 0; i < 5; i++)
        {
            root.Advance(1 / 60.0);
            root.Update(Viewport);
        }
    }

    private static IEnumerable<SemanticsNode> All(SemanticsNode node) => node.Children.SelectMany(All).Prepend(node);

    private static IEnumerable<SemanticsNode> All(UIRoot root) => All(root.GetSemantics());

    private static string Number(int i) => i.ToString(CultureInfo.InvariantCulture);

    private static SemanticsNode? Row(UIRoot root, int index) =>
        All(root).FirstOrDefault(n => n.Role == SemanticsRole.Row && n.Semantics.Value == Number(index + 1));

    private static HashSet<int> SelectedRows(UIRoot root) =>
        [.. All(root).Where(n => n.Role == SemanticsRole.Row && n.Semantics.Selected).Select(n => int.Parse(n.Semantics.Value!, CultureInfo.InvariantCulture) - 1)];

    private static Vector2 Centre(SemanticsNode node) => new(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);

    private static void Click(UIRoot root, Vector2 at, KeyModifiers modifiers = KeyModifiers.None)
    {
        root.PointerMove(at);
        root.PointerDown(at, PointerButton.Left, modifiers);
        root.PointerUp(at, PointerButton.Left, modifiers);
        Settle(root);
    }

    private static void Press(UIRoot root, KeyCode key, KeyModifiers modifiers = KeyModifiers.None)
    {
        root.KeyDown(key, modifiers);
        Settle(root);
    }

    private static DataColumn[] People =>
    [
        new("Name", i => new SurfaceText("Person " + Number(i))) { Width = 200, Sortable = true },
        new("Age", i => new SurfaceText(Number(20 + i % 60))) { Width = 80, Sortable = true, Alignment = Radiant.Text.TextAlignment.End },
        new("City", i => new SurfaceText(i % 2 == 0 ? "London" : "Paris")) { Grow = true },
    ];

    [TestMethod]
    public void AHundredThousandRowsShowAScreenful()
    {
        var watch = Stopwatch.StartNew();
        using var root = Mount(new DataTable(People, 100_000) { Label = "People" });
        var mounted = watch.Elapsed;

        var rows = All(root).Count(n => n.Role == SemanticsRole.Row && n.Semantics.Value is not null);

        Assert.IsTrue(rows is > 5 and < 20, $"{rows} rows built");
        Assert.AreEqual("Person 0 20 London", Row(root, 0)!.Label, "a row is named by its cells");
        Assert.IsNotNull(All(root).SingleOrDefault(n => n.Role == SemanticsRole.Table && n.Label == "People"));
        Assert.IsTrue(mounted.TotalMilliseconds < 500, $"{mounted.TotalMilliseconds} ms");
    }

    [TestMethod]
    public void SortableHeadersSortOneWayThenTheOther()
    {
        var sorts = new List<(int, SortDirection)>();
        using var root = Mount(new Host(ctx => new DataTable(People, 10)
        {
            SortColumn = sorts.Count == 0 ? null : sorts[^1].Item1,
            SortDirection = sorts.Count == 0 ? SortDirection.Ascending : sorts[^1].Item2,
            OnSort = (c, d) => sorts.Add((c, d)),
        }));
        var header = All(root).Single(n => n.Role == SemanticsRole.ColumnHeader && n.Label == "Age");

        Click(root, Centre(header));
        root.Dispose();
        using var sorted = Mount(new DataTable(People, 10) { SortColumn = 1, SortDirection = SortDirection.Ascending, OnSort = (c, d) => sorts.Add((c, d)) });
        Click(sorted, Centre(All(sorted).Single(n => n.Role == SemanticsRole.ColumnHeader && n.Label == "Age")));

        CollectionAssert.AreEqual(new[] { (1, SortDirection.Ascending), (1, SortDirection.Descending) }, sorts);
    }

    [TestMethod]
    public void APressSelectsOneRow()
    {
        using var root = Mount(new DataTable(People, 50));

        Click(root, Centre(Row(root, 2)!));
        Click(root, Centre(Row(root, 4)!));

        CollectionAssert.AreEquivalent(new[] { 4 }, SelectedRows(root).ToArray());
    }

    [TestMethod]
    public void CommandAddsAndShiftSelectsARange()
    {
        using var root = Mount(new DataTable(People, 50) { MultiSelect = true });

        Click(root, Centre(Row(root, 1)!));
        Click(root, Centre(Row(root, 3)!), Command);
        Click(root, Centre(Row(root, 6)!), KeyModifiers.Shift);
        var range = SelectedRows(root);
        Click(root, Centre(Row(root, 4)!), Command);

        CollectionAssert.AreEquivalent(new[] { 3, 4, 5, 6 }, range.ToArray(), "Shift reaches from the last row chosen");
        CollectionAssert.AreEquivalent(new[] { 3, 5, 6 }, SelectedRows(root).ToArray());
    }

    [TestMethod]
    public void CheckBoxesToggleRowsAndTheHeaderSelectsAll()
    {
        var reported = new List<int>();
        using var root = Mount(new DataTable(People, 5) { MultiSelect = true, ShowCheckboxes = true, OnSelectionChange = s => reported.Add(s.Count) });
        SemanticsNode Box(int row) => All(root).Where(n => n.Role == SemanticsRole.CheckBox && n.Label == "Select row").ElementAt(row);

        Click(root, Centre(Box(0)));
        Click(root, Centre(Box(2)));
        var partial = All(root).Single(n => n.Role == SemanticsRole.CheckBox && n.Label == "Select all").Semantics.Checked;
        Click(root, Centre(All(root).Single(n => n.Role == SemanticsRole.CheckBox && n.Label == "Select all")));

        CollectionAssert.AreEqual(new[] { 1, 2, 5 }, reported);
        Assert.IsNull(partial, "some chosen: the header box is indeterminate");
        Assert.AreEqual(5, SelectedRows(root).Count);
    }

    [TestMethod]
    public void TheKeyboardMovesThroughEveryRow()
    {
        var activated = new List<int>();
        using var root = Mount(new DataTable(People, 100_000) { MultiSelect = true, OnActivate = activated.Add });
        Click(root, Centre(Row(root, 0)!));

        Press(root, KeyCode.Down);
        Press(root, KeyCode.Down, KeyModifiers.Shift);
        var extended = SelectedRows(root);
        Press(root, KeyCode.End);
        var last = Row(root, 99_999);
        Press(root, KeyCode.Enter);
        Press(root, KeyCode.A, Command);

        CollectionAssert.AreEquivalent(new[] { 1, 2 }, extended.ToArray());
        Assert.IsNotNull(last, "End scrolls the last row into view");
        Assert.IsTrue(last!.Semantics.Selected);
        CollectionAssert.AreEqual(new[] { 99_999 }, activated);
        Assert.IsTrue(SelectedRows(root).Count > 5, "select all selects every row built");
    }

    [TestMethod]
    public void ADoubleClickActivatesARow()
    {
        var activated = new List<int>();
        using var root = Mount(new DataTable(People, 10) { OnActivate = activated.Add });
        var at = Centre(Row(root, 3)!);

        root.PointerDown(at);
        root.PointerUp(at);
        root.PointerDown(at);
        root.PointerUp(at);

        CollectionAssert.AreEqual(new[] { 3 }, activated);
    }

    [TestMethod]
    public void DraggingAHeadersEdgeResizesItsColumn()
    {
        using var root = Mount(new DataTable(People, 10));
        float Age() => All(root).Single(n => n.Role == SemanticsRole.ColumnHeader && n.Label == "Age").Bounds.X;
        var grip = All(root).Single(n => n.Label == "Resize Name");
        var ageBefore = Age();

        root.PointerDown(Centre(grip));
        root.PointerMove(Centre(grip) + new Vector2(60, 0));
        root.PointerUp(Centre(grip) + new Vector2(60, 0));
        Settle(root);
        var wider = Age();
        grip = All(root).Single(n => n.Label == "Resize Name");
        root.PointerDown(Centre(grip));
        root.PointerMove(Centre(grip) - new Vector2(500, 0));
        root.PointerUp(Centre(grip) - new Vector2(500, 0));
        Settle(root);

        Assert.AreEqual(ageBefore + 60, wider, 1f, "the next column moves over");
        Assert.AreEqual(ageBefore - 200 + 60, Age(), 1f, "held at its 60 px minimum");
    }

    [TestMethod]
    public void AnEmptyTableSaysSo()
    {
        using var root = Mount(new DataTable(People, 0) { EmptyText = "No people" });

        Assert.IsTrue(All(root).Any(n => n.Label == "No people"), string.Join(" | ", All(root).Select(n => n.Role + ":" + n.Label)));
        Assert.IsTrue(All(root).Any(n => n.Label == "Name"), "the header still shows");
    }

    [TestMethod]
    public void AnOwnedSelectionIsReportedNotKept()
    {
        var reported = new List<IReadOnlySet<int>>();
        using var root = Mount(new DataTable(People, 10) { Selection = new HashSet<int> { 1 }, OnSelectionChange = reported.Add });

        Click(root, Centre(Row(root, 5)!));

        CollectionAssert.AreEquivalent(new[] { 5 }, reported.Single().ToArray());
        CollectionAssert.AreEquivalent(new[] { 1 }, SelectedRows(root).ToArray());
    }

    private sealed record Host(Func<BuildContext, Element?> Body) : Component
    {
        public override Element? Build(BuildContext context) => Body(context);
    }
}
