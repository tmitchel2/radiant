using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Gallery.ThemeLab;

/// <summary>
/// The theme page: the editor beside a preview of the components, each scrolling on its own, so
/// an edit shows while the control that made it stays in view. The rest of the gallery changes too.
/// </summary>
/// <param name="Themes">The app's theme.</param>
internal sealed record ThemePage(ThemeController Themes) : Component
{
    public override Element? Build(BuildContext context) => new Splitter(
        new Surface
        {
            SurfaceColor = SurfaceName.SurfaceContainerLow,
            CornerShape = CornerShapeRole.Large,
            ClipContent = true,
            Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1, MinHeight = 0, Padding = new Edges(16, 16, 4, 0) },
            Children = [new ThemeEditor(Themes)],
        },
        new ScrollArea
        {
            Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1, MinHeight = 0 },
            ContentLayout = new LayoutStyle { Padding = new Edges(16, 0, 8, 16) },
            Children = [new ThemePreview()],
        })
    {
        InitialSize = 420,
        MinSize = 320,
        MinOtherSize = 280,
        Label = "Theme editor",
        Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1, MinHeight = 0 },
    };
}
