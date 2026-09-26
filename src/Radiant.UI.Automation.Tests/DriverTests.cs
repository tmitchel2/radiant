using System.Numerics;
using System.Text.Json;
using Radiant.Host.AgentControlProtocol;
using Radiant.UI.Driver;
using Radiant.UI.Driver.MSTest;

namespace Radiant.UI.Automation.Tests;

[TestClass]
public sealed class DriverTests : RadiantUITest
{
    [TestInitialize]
    public void Start() => Use(AppDriver.InProcess(FormApp.Themed(), new InProcessOptions { Size = new Vector2(800, 700), AppName = "form" }));

    [TestMethod]
    public async Task TappingAButtonWaitsForTheAppAndActs()
    {
        var result = await Driver.FormApp().Save().TapAsync();

        await Driver.FormApp().Status().Expect().ToHaveTextAsync("Saved 1 times");
        Assert.AreEqual(FormApp.Save, result.Target!.TestId);
        Assert.IsTrue(result.Idle, "a spinner doesn't keep the app busy");
        Assert.IsNotNull(result.At);
    }

    [TestMethod]
    public async Task SelectorsInTheCompactSyntaxFindTheSameThings()
    {
        await Driver.Get("role=button label=Save").TapAsync();
        await Driver.Get("text=Save").TapAsync();

        await Driver.Get("@FormApp.Status").Expect().ToContainTextAsync("saved 2");
    }

    [TestMethod]
    public async Task TypingGoesIntoTheField()
    {
        var typed = await Driver.FormApp().Name().TypeAsync("Ada");
        await Driver.FormApp().Name().FillAsync("Grace");

        Assert.AreEqual("Ada", typed.Value);
        await Driver.FormApp().Name().Expect().ToHaveValueAsync("Grace");
    }

    [TestMethod]
    public async Task CheckBoxesCheck()
    {
        await Driver.FormApp().Agree().Expect().ToBeUncheckedAsync();

        await Driver.FormApp().Agree().TapAsync();

        await Driver.FormApp().Agree().Expect().ToBeCheckedAsync();
    }

    [TestMethod]
    public async Task ActingOnARowOutOfViewScrollsItIn()
    {
        var before = await Driver.FormApp().Row().WithLabel("Row 45").InspectAsync("visibility");
        Assert.IsNull(before.Visible, "it starts out of view");

        await Driver.FormApp().Row().WithLabel("Row 45").TapAsync();

        await Driver.FormApp().Picked().Expect().ToHaveTextAsync("Picked row 45");
        var list = await Driver.FormApp().List().InspectAsync("scroll");
        Assert.IsTrue(list.Scroll!.Offset!.Y > 0);
        Assert.AreEqual(1f, (await Driver.FormApp().Row().WithLabel("Row 45").InspectAsync("visibility")).VisibleRatio);
    }

    [TestMethod]
    public async Task InspectGivesWhereToTap()
    {
        var node = await Driver.FormApp().Save().InspectAsync("basic,geometry,hit");

        Assert.AreEqual("button", node.Role);
        Assert.AreEqual("Save", node.Label);
        Assert.IsTrue(node.Hittable);
        Assert.IsTrue(node.Tap!.X >= node.Bounds!.X && node.Tap.X <= node.Bounds.X + node.Bounds.W);
        Assert.IsTrue(node.Tap.Y >= node.Bounds.Y && node.Tap.Y <= node.Bounds.Y + node.Bounds.H);
        Assert.IsNull(node.Checked, "fields not asked for are left out");
    }

    [TestMethod]
    public async Task TappingThePointInspectGaveHitsTheElement()
    {
        var node = await Driver.FormApp().Save().InspectAsync("hit");

        await Driver.TapAtAsync(new Vector2(node.Tap!.X, node.Tap.Y));

        await Driver.FormApp().Status().Expect().ToHaveTextAsync("Saved 1 times");
    }

    [TestMethod]
    public async Task NamedFieldsAreWrittenEvenWhenEmpty()
    {
        var node = await Driver.FormApp().Save().InspectAsync("id,checked,disabled,-id");

        Assert.IsNull(node.Id);
        Assert.AreEqual(false, node.Disabled);
    }

    [TestMethod]
    public async Task TheTreeGoesAsDeepAsAsked()
    {
        var shallow = await Driver.TreeAsync("basic", depth: 1);
        var deep = await Driver.TreeAsync("basic,scroll", visibleOnly: true);

        var root = shallow.Nodes.Single();
        Assert.IsTrue(root.Children!.All(c => c.Children is null), "one level only");
        Assert.IsTrue(root.Children!.Any(c => c.ChildCount > 0), "where it stops it counts");
        Assert.AreEqual("semantics", deep.Tree);
        var all = Flatten(deep.Nodes.Single()).ToList();
        Assert.IsTrue(all.Any(n => n.TestId == FormApp.List && n.Scroll is not null));
        Assert.IsFalse(all.Any(n => n.Label == "Row 45"), "visibleOnly leaves out rows out of view");
    }

    [TestMethod]
    public async Task ADialogCoversWhatsBehindIt()
    {
        await Driver.FormApp().Open().TapAsync();
        await Driver.FormApp().Confirm().Panel().Expect().ToBeVisibleAsync();

        var covered = await Driver.FormApp().Save().InspectAsync("hit");
        var error = await Assert.ThrowsAsync<AppDriverException>(() =>
            Driver.CallAsync("ui.tap", JsonDocument.Parse("""{"selector":"@FormApp.Save"}""").RootElement, TimeSpan.FromMilliseconds(300)));

        Assert.IsFalse(covered.Hittable);
        Assert.IsNotNull(covered.ObscuredBy);
        Assert.AreEqual(AgentErrorCodes.NotHittable, error.Code);

        await Driver.FormApp().Close().TapAsync();
        await Driver.FormApp().Confirm().Panel().Expect().ToBeGoneAsync();
        await Driver.FormApp().Save().Expect().ToBeHittableAsync();
    }

    [TestMethod]
    public async Task NoMatchSuggestsWhatWasMeant()
    {
        var error = await Assert.ThrowsAsync<AppDriverException>(() =>
            Driver.CallAsync("ui.tap", JsonDocument.Parse("""{"selector":"@FormApp.Sav"}""").RootElement, TimeSpan.FromMilliseconds(200)));

        Assert.AreEqual(AgentErrorCodes.NoMatch, error.Code);
        StringAssert.Contains(error.Message, "@FormApp.Save");
        StringAssert.Contains(error.Message, "What happened last", "the log's end comes with it");
    }

    [TestMethod]
    public async Task SeveralMatchesAreAmbiguousUntilPicked()
    {
        var error = await Assert.ThrowsAsync<AppDriverException>(() => Driver.Role.ListItem().TapAsync());

        Assert.AreEqual(AgentErrorCodes.Ambiguous, error.Code);
        await Driver.FormApp().Row().Nth(3).TapAsync();
        await Driver.FormApp().Picked().Expect().ToHaveTextAsync("Picked row 3");
        Assert.AreEqual(60, await Driver.Role.ListItem().CountAsync());
    }

    [TestMethod]
    public async Task WithinNarrowsToWhatsInside()
    {
        var rows = await Driver.FormApp().List().Role.ListItem(TextMatch.Contains("row 1")).QueryAsync();

        Assert.AreEqual(11, rows.Count, "row 1 and rows 10 to 19");
    }

    [TestMethod]
    public async Task GeneratedLocatorsNarrowByStateAndContent()
    {
        await Driver.FormApp().Agree().TapAsync();
        await Driver.FormApp().Open().TapAsync();

        await Driver.FormApp().Agree().Checked().Expect().ToExistAsync();
        await Driver.FormApp().Confirm().Containing("Are you sure?").Panel().Expect().ToBeVisibleAsync();
        await Driver.FormApp().Confirm().Containing("Not in it").Expect().ToBeGoneAsync();
        Assert.AreEqual(1, await Driver.FormApp().Confirm().Role.Button("Close").CountAsync(), "roles within a component");
        Assert.AreEqual(60, await Driver.FormApp().Row().CountAsync(), "one part, many rows");
    }

    [TestMethod]
    public async Task KeysMoveFocus()
    {
        var focused = await Driver.FormApp().Name().FocusAsync();
        await Driver.FormApp().Name().Expect().ToBeFocusedAsync();

        var after = await Driver.KeyAsync("Tab");

        Assert.AreNotEqual(0, focused.FocusedId);
        Assert.AreNotEqual(focused.FocusedId, after.FocusedId);
    }

    [TestMethod]
    public async Task ScrollingByAndToEdges()
    {
        var down = await Driver.FormApp().List().ScrollAsync(new Vector2(0, 100));
        var bottom = await Driver.FormApp().List().ScrollToAsync("bottom");

        Assert.AreEqual(100, down.Scroll!.Offset.Y, 0.5f);
        Assert.AreEqual(bottom.Scroll!.Max.Y, bottom.Scroll.Offset.Y, 0.5f);
        await Driver.FormApp().Row().WithLabel("Row 59").Expect().ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task SteppingMovesUITimeOn()
    {
        var before = await Driver.InfoAsync();

        var after = await Driver.StepAsync(TimeSpan.FromMilliseconds(500));

        Assert.AreEqual("fixed", before.Clock);
        Assert.IsTrue(after.Time - before.Time >= 0.49, $"{before.Time} → {after.Time}");
    }

    [TestMethod]
    public async Task IdleListsWhatsStillMovingThatDoesNotCount()
    {
        var idle = await Driver.WaitForIdleAsync();

        CollectionAssert.Contains(idle.Continuous, "circular progress");
    }

    [TestMethod]
    public async Task TheLogRecordsTestsActionsAndPeoplesInput()
    {
        await Driver.FormApp().Save().TapAsync();
        var save = await Driver.FormApp().Save().InspectAsync("hit");
        // Input given to the app directly, as a window gives it: a person's.
        var session = Driver.Session!;
        session.Root.PointerDown(new Vector2(save.Tap!.X, save.Tap.Y));
        session.Root.PointerUp(new Vector2(save.Tap.X, save.Tap.Y));
        session.Step();
        await Driver.NoteAsync("checked by hand");

        var log = await Driver.LogTailAsync();

        var action = log.Single(e => e.Kind == LogKinds.Action && e.Action!.Name == "ui.tap");
        Assert.AreEqual(LogSources.Test, action.Src);
        Assert.AreEqual("@" + FormApp.Save, action.Action!.Selector);
        var result = log.Single(e => e.Kind == LogKinds.Result && e.Action!.Name == "ui.tap");
        Assert.AreEqual(FormApp.Save, result.Target!.TestId);
        var human = log.Single(e => e.Src == LogSources.Human);
        Assert.AreEqual("tap", human.Input!.Type);
        Assert.AreEqual(FormApp.Save, human.Target!.TestId);
        Assert.IsTrue(log.Any(e => e.Kind == LogKinds.Note && e.Note == "checked by hand"));
        StringAssert.Contains(LogFormatter.Format(human), "button \"Save\" @FormApp.Save");
    }

    [TestMethod]
    public async Task TheLogStreams()
    {
        using var stop = new CancellationTokenSource();
        var entries = new List<LogEntry>();
        var reading = Task.Run(async () =>
        {
            await foreach (var entry in Driver.LogAsync("test", since: 0, stop.Token))
            {
                entries.Add(entry);
                if (entry.Kind == LogKinds.Result)
                {
                    await stop.CancelAsync();
                }
            }
        });
        await Task.Delay(50);
        await Driver.FormApp().Save().TapAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => reading.WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.AreEqual(LogKinds.Action, entries[0].Kind);
        Assert.AreEqual(LogKinds.Result, entries[^1].Kind);
        Assert.IsTrue(entries.All(e => e.Src == LogSources.Test), "what the test's tap changed is put down to the test");
    }

    [TestMethod]
    public async Task ActionsListDescribesTheirParams()
    {
        var actions = (await Driver.CallAsync("actions.list")).Deserialize(AgentJsonContext.Default.ActionDefinitionArray)!;

        var tap = actions.Single(a => a.Name == "ui.tap");
        StringAssert.Contains(tap.ParamsSchema, "\"selector\"");
        Assert.IsTrue(actions.Any(a => a.Name == "ui.screenshot"));
    }

    [TestMethod]
    [TestCategory("Gpu")]
    public async Task AnnotatedScreenshotsNumberWhatCanBeOperated()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radiant-shot-{Guid.NewGuid():N}.png");
        ScreenshotInfo shot;
        try
        {
            shot = await Driver.ScreenshotAsync(path, "interactive");
        }
        catch (AppDriverException e) when (e.Code == AgentErrorCodes.Unsupported)
        {
            Assert.Inconclusive("No GPU.");
            return;
        }
        try
        {
            Assert.IsTrue(File.Exists(shot.Path));
            Assert.AreEqual(800, shot.Width);
            var save = shot.Marks!.Single(m => m.TestId == FormApp.Save);
            Assert.IsNotNull(save.Tap);
            Assert.IsFalse(shot.Marks!.Any(m => m.Label == "Row 45"), "out of view, so unmarked");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static IEnumerable<InspectNode> Flatten(InspectNode node) => (node.Children ?? []).SelectMany(Flatten).Prepend(node);
}
