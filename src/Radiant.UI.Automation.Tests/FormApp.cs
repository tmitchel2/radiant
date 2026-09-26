using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.UI.Automation.Tests;

/// <summary>A small app with the things tests act on: a button, a field, a check box, a long list, a dialog, a spinner.</summary>
internal sealed partial record FormApp : Component
{
    [TestId<SurfaceButton>] public static partial string Save { get; }
    [TestId] public static partial string Status { get; }
    [TestId<TextField>] public static partial string Name { get; }
    [TestId<Checkbox>] public static partial string Agree { get; }
    [TestId<SurfaceButton>] public static partial string Open { get; }
    [TestId] public static partial string Spinner { get; }
    [TestId] public static partial string Picked { get; }
    [TestId] public static partial string List { get; }
    [TestId] public static partial string Row { get; }
    [TestId<Dialog>] public static partial string Confirm { get; }
    [TestId<SurfaceButton>] public static partial string Close { get; }

    public static Element Themed() => new ThemeProvider(new ThemeController(), new FormApp());

    public override Element? Build(BuildContext context)
    {
        var saves = context.UseState(0);
        var name = context.UseState(TextEditState.Empty);
        var agreed = context.UseState(false);
        var dialog = context.UseState(false);
        var picked = context.UseState((string?)null);
        return new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1, Padding = Edges.All(16), RowGap = 8 },
            Children =
            [
                new SurfaceButton("Save") { TestId = Save, OnPress = () => saves.Set(saves.Value + 1) },
                new TextBlock($"Saved {saves.Value} times") { TestId = Status },
                new TextField("Name") { TestId = Name, Value = name.Value, OnChange = name.Set },
                new Checkbox(agreed.Value, agreed.Set) { TestId = Agree },
                new SurfaceButton("Open dialog") { TestId = Open, OnPress = () => dialog.Set(true) },
                new CircularProgress { TestId = Spinner },
                new TextBlock(picked.Value is { } row ? $"Picked {row}" : "Nothing picked") { TestId = Picked },
                new ScrollArea
                {
                    TestId = List,
                    Layout = new LayoutStyle { Height = 200 },
                    Children = [.. Enumerable.Range(0, 60).Select(i => (Element?)new Box
                    {
                        Key = i,
                        TestId = Row,
                        Focusable = true,
                        Semantics = new Semantics { Role = SemanticsRole.ListItem, Label = $"Row {i}" },
                        Layout = new LayoutStyle { Height = 24 },
                        OnClick = _ => picked.Set($"row {i}"),
                    })],
                },
                new Dialog(dialog.Value, () => dialog.Set(false))
                {
                    TestId = Confirm,
                    Title = "Confirm",
                    Text = "Are you sure?",
                    Actions = [new SurfaceButton("Close") { TestId = Close, OnPress = () => dialog.Set(false) }],
                },
            ],
        };
    }
}
