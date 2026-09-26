using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.UI.Core.Tests;

[TestClass]
public class CommandTests
{
    private static readonly Vector2 Viewport = new(400, 300);

    private static readonly KeyChord Save = KeyChord.Command(KeyCode.S);

    private static Element Registers(Command command) => new Lambda(context =>
    {
        context.UseCommand(command);
        return null;
    });

    [TestMethod]
    public void ARegisteredCommandIsListedAndAnswersToItsShortcut()
    {
        var log = new Log();
        using var root = new UIRoot(Registers(new Command("save", "Save") { Shortcut = Save, Run = () => log.Add("save") }));
        root.Update(Viewport);

        root.KeyDown(Save.Key, Save.Modifiers);

        CollectionAssert.AreEqual(new[] { "save" }, log.Entries);
        Assert.AreEqual("Save", root.Commands.Commands.Single().Title);
        Assert.IsTrue(root.Commands.Execute("save"));
        Assert.AreEqual(2, log.Entries.Count);
    }

    [TestMethod]
    public void ADisabledCommandDoesNothing()
    {
        var log = new Log();
        using var root = new UIRoot(Registers(new Command("save", "Save") { Shortcut = Save, Enabled = false, Run = () => log.Add("save") }));
        root.Update(Viewport);

        root.KeyDown(Save.Key, Save.Modifiers);

        Assert.IsFalse(root.Commands.Execute("save"));
        Assert.AreEqual(0, log.Entries.Count);
    }

    [TestMethod]
    public void ACommandGoesWithItsComponent()
    {
        var shown = new Signal<bool>(true);
        var log = new Log();
        using var root = new UIRoot(new Lambda(context => context.Watch(shown)
            ? Registers(new Command("save", "Save") { Shortcut = Save, Run = () => log.Add("save") })
            : null));
        root.Update(Viewport);

        shown.Value = false;
        root.Update(Viewport);
        root.KeyDown(Save.Key, Save.Modifiers);

        Assert.AreEqual(0, root.Commands.Commands.Count);
        Assert.AreEqual(0, log.Entries.Count);
    }

    [TestMethod]
    public void ADeeperCommandWithTheSameIdTakesOverUntilItGoes()
    {
        var editing = new Signal<bool>(true);
        var log = new Log();
        using var root = new UIRoot(new Box
        {
            Children =
            [
                Registers(new Command("copy", "Copy") { Run = () => log.Add("app") }),
                Registers(new Command("other", "Other")),
                new Lambda(context => context.Watch(editing) ? Registers(new Command("copy", "Copy text") { Run = () => log.Add("editor") }) : null),
            ],
        });
        root.Update(Viewport);

        root.Commands.Execute("copy");
        CollectionAssert.AreEqual(new[] { "Copy text", "Other" }, root.Commands.Commands.Select(c => c.Title).ToArray());
        editing.Value = false;
        root.Update(Viewport);
        root.Commands.Execute("copy");

        CollectionAssert.AreEqual(new[] { "editor", "app" }, log.Entries);
        CollectionAssert.AreEqual(new[] { "Copy", "Other" }, root.Commands.Commands.Select(c => c.Title).ToArray());
    }

    [TestMethod]
    public void ListersRebuildWhenACommandChangesButNotForANewClosure()
    {
        var enabled = new Signal<bool>(true);
        var tick = new Signal<int>(0);
        var builds = 0;
        IReadOnlyList<Command> listed = [];
        var ran = new List<int>();
        using var root = new UIRoot(new Box
        {
            Children =
            [
                new Lambda(context =>
                {
                    var at = context.Watch(tick);
                    context.UseCommand(new Command("go", "Go") { Enabled = context.Watch(enabled), Run = () => ran.Add(at) });
                    return null;
                }),
                new Lambda(context =>
                {
                    builds++;
                    listed = context.UseCommands();
                    return null;
                }),
            ],
        });
        root.Update(Viewport);
        var before = builds;

        tick.Value = 1;
        root.Update(Viewport);
        Assert.AreEqual(before, builds, "a new closure alone isn't a change");
        root.Commands.Execute("go");
        CollectionAssert.AreEqual(new[] { 1 }, ran, "it runs the latest build's action");

        enabled.Value = false;
        root.Update(Viewport);
        Assert.IsTrue(builds > before);
        Assert.IsFalse(listed.Single().Enabled);
    }

    [TestMethod]
    public void CommandsAreListedInTreeOrder()
    {
        using var root = new UIRoot(new Lambda(context =>
        {
            context.UseCommand(new Command("a", "A"));
            context.UseCommand(new Command("b", "B"));
            return new Box
            {
                Children =
                [
                    new Lambda(inner =>
                    {
                        inner.UseCommand(new Command("c", "C"));
                        inner.UseCommand(new Command("d", "D"));
                        return null;
                    }),
                    Registers(new Command("e", "E")),
                ],
            };
        }));

        root.Update(Viewport);

        CollectionAssert.AreEqual(new[] { "A", "B", "C", "D", "E" }, root.Commands.Commands.Select(c => c.Title).ToArray());
    }
}
