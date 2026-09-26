using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.UI.Automation.Tests;

/// <summary>A small app with the things tests act on: a button, a field, a check box, a long list, a dialog, a spinner.</summary>
internal sealed record FormApp : Component
{
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
                new SurfaceButton("Save") { TestId = "save", OnPress = () => saves.Set(saves.Value + 1) },
                new TextBlock($"Saved {saves.Value} times") { TestId = "status" },
                new TextField("Name") { TestId = "name", Value = name.Value, OnChange = name.Set },
                new Checkbox(agreed.Value, agreed.Set) { TestId = "agree" },
                new SurfaceButton("Open dialog") { TestId = "open", OnPress = () => dialog.Set(true) },
                new CircularProgress { TestId = "spinner" },
                new TextBlock(picked.Value is { } row ? $"Picked {row}" : "Nothing picked") { TestId = "picked" },
                new ScrollArea
                {
                    TestId = "list",
                    Layout = new LayoutStyle { Height = 200 },
                    Children = [.. Enumerable.Range(0, 60).Select(i => (Element?)new Box
                    {
                        Key = i,
                        TestId = $"row-{i}",
                        Focusable = true,
                        Semantics = new Semantics { Role = SemanticsRole.ListItem, Label = $"Row {i}" },
                        Layout = new LayoutStyle { Height = 24 },
                        OnClick = _ => picked.Set($"row {i}"),
                    })],
                },
                new Dialog(dialog.Value, () => dialog.Set(false))
                {
                    Title = "Confirm",
                    Text = "Are you sure?",
                    Actions = [new SurfaceButton("Close") { TestId = "close", OnPress = () => dialog.Set(false) }],
                },
            ],
        };
    }
}
