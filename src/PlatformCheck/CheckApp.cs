using System;
using System.Linq;
using System.Threading.Tasks;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Platform;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.PlatformCheck;

/// <summary>
/// One window exercising each platform service by hand: an input method field, every cursor,
/// the live appearance settings (which the theme also follows), the clipboard and the dialogs.
/// </summary>
internal sealed record CheckApp(ImeFieldModel Field) : Component
{
    private static readonly FileFilter[] s_filters = [new("Images", ["png", "jpg"]), new("Text", ["txt", "md"])];

    public override Element? Build(BuildContext context)
    {
        var theme = context.UseTheme();
        var platform = context.UsePlatform();
        var status = context.UseState("Ready.");
        var appearanceChanges = context.UseState(0);
        var appearance = platform.Appearance;
        context.Watch(Field.Version);
        context.UseEffect(() =>
        {
            Action changed = () => appearanceChanges.Update(n => n + 1);
            appearance.Changed += changed;
            return () => appearance.Changed -= changed;
        }, appearance);
        var field = Field;

        // The app draws its own title bar: drag it to move the window, double-click it to zoom.
        return new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1 },
            Background = theme.Background,
            Children =
            [
                new TitleBar("Radiant platform check")
                {
                    Trailing = [new IconButton("info", "About") { OnPress = () => status.Set("The title bar's buttons press without moving the window.") }],
                },
                new ScrollArea
                {
                    Layout = new LayoutStyle { FlexGrow = 1 },
                    ContentLayout = new LayoutStyle { Padding = Edges.All(24), RowGap = 16 },
                    Children = [.. Body()],
                },
            ],
        };

        Element?[] Body() =>
        [
            new SurfaceText($"Radiant platform check ({platform.Name})") { TextType = TextType.HeadlineSmall },
            Section("Context menu",
                "Right-click (or Control-click) the box: the menu should be the system's, with a separator before "
                + "Delete and Paste greyed out. Choosing an item shows it in the status line.",
                new ContextMenu(new Surface
                {
                    SurfaceColor = SurfaceName.SurfaceContainerHighest,
                    CornerShape = CornerShapeRole.Medium,
                    Layout = new LayoutStyle { Height = 80, AlignItems = Align.Center, JustifyContent = Justify.Center },
                    Children = [new SurfaceText("Right-click here") { Legibility = Legibility.Medium }],
                },
                [
                    new MenuItem("Cut", () => status.Set("Chose Cut.")),
                    new MenuItem("Copy", () => status.Set("Chose Copy.")),
                    new MenuItem("Paste") { Disabled = true },
                    new MenuItem("Delete", () => status.Set("Chose Delete.")) { DividerBefore = true },
                ])),
            Section("Input method",
                "Switch to a Japanese (Romaji) or Chinese (Pinyin) input source, type in the field, and check the "
                + "candidate window opens under the underlined text. Space converts, Enter commits, Escape cancels.",
                new ImeField(field),
                new SurfaceText(field.Log.Count == 0 ? "No input yet." : string.Join("  ·  ", field.Log))
                {
                    TextType = TextType.BodySmall,
                    Legibility = Legibility.Medium,
                }),
            Section("Cursors", "Hover each tile.",
                new Row([.. Enum.GetValues<CursorShape>().Select(shape => (Element?)new CursorSwatch(shape))])),
            Section("Appearance",
                "Change dark mode, the accent colour, or Accessibility › Display settings; the theme follows.",
                new SurfaceText(
                    $"Dark {appearance.IsDark}  ·  accent #{appearance.AccentColor & 0xFFFFFF:X6}  ·  "
                    + $"increase contrast {appearance.IncreaseContrast}  ·  reduce motion {appearance.ReduceMotion}  ·  "
                    + $"{appearanceChanges.Value} changes")),
            Section("Clipboard and dialogs", status.Value,
                new Row(
                    new SurfaceButton("Copy field", ButtonVariant.Tonal)
                    {
                        OnPress = () =>
                        {
                            platform.Clipboard.SetText(field.Text);
                            status.Set($"Copied \"{field.Text}\".");
                        },
                    },
                    new SurfaceButton("Paste into field", ButtonVariant.Tonal)
                    {
                        OnPress = () =>
                        {
                            var text = platform.Clipboard.GetText();
                            if (text is not null)
                            {
                                field.Replace(text);
                            }
                            status.Set(text is null ? "The clipboard has no text." : $"Pasted \"{text}\".");
                        },
                    },
                    new SurfaceButton("Open…", ButtonVariant.Outlined) { OnPress = () => _ = Open(platform, status) },
                    new SurfaceButton("Save…", ButtonVariant.Outlined) { OnPress = () => _ = Save(platform, status) })),
        ];
    }

    private static Card Section(string title, string description, params Element?[] content) =>
        new Card([
            new SurfaceText(title) { TextType = TextType.TitleMedium },
            new SurfaceText(description) { TextType = TextType.BodyMedium, Legibility = Legibility.Medium },
            .. content,
        ])
        {
            Layout = new LayoutStyle { Padding = Edges.All(16), RowGap = 10 },
        };

    private static async Task Open(IPlatform platform, State<string> status)
    {
        var paths = await platform.Dialogs.OpenAsync(new OpenFileOptions
        {
            Title = "Pick images or text files",
            AllowMultiple = true,
            Filters = s_filters,
        });
        status.Set(paths.Count == 0 ? "Open cancelled." : $"Opened {string.Join(", ", paths)}");
    }

    private static async Task Save(IPlatform platform, State<string> status)
    {
        var path = await platform.Dialogs.SaveAsync(new SaveFileOptions { Title = "Save a check file", SuggestedName = "Check.txt", Filters = [s_filters[1]] });
        status.Set(path is null ? "Save cancelled." : $"Would save to {path}");
    }
}
