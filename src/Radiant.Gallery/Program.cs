using System;
using System.Numerics;
using Radiant.ColorSystem;
using Radiant.Gallery;
using Radiant.Graphics;
using Radiant.Graphics2D;
using Radiant.Platform.MacOS;
using Radiant.Theming;
using Radiant.UI.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

// radiant-gallery                         opens the gallery in a window, following the system appearance
// radiant-gallery --snapshot out.png [--dark] [--seed #rrggbb] [--variant Vibrant] [--scale 2] [--height 1400] [--page 0-11] [--dialog] [--menu] [--palette] [--sheet] [--rtl]
//                                         renders it offscreen to a PNG instead
var theme = new Theme();
string? snapshot = null;
var startWithDialog = false;
var startWithPalette = false;
var startWithSheet = false;
var bench = 0;
var startWithMenu = false;
var startPage = 0;
var followSystem = true;
var scale = 1f;
var height = 640;
var rightToLeft = false;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--snapshot": snapshot = args[++i]; break;
        case "--dark": theme = theme with { Colors = theme.Colors with { IsDark = true } }; followSystem = false; break;
        case "--seed": theme = theme with { Colors = theme.Colors with { Seed = Radiant.Graphics2D.Color.Parse(args[++i]) } }; followSystem = false; break;
        case "--variant": theme = theme with { Colors = theme.Colors with { Variant = Enum.Parse<Variant>(args[++i]) } }; break;
        case "--height": height = int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--dialog": startWithDialog = true; break;
        case "--palette": startWithPalette = true; break;
        case "--rtl": rightToLeft = true; break;
        case "--sheet": startWithSheet = true; break;
        case "--bench": bench = int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--menu": startWithMenu = true; break;
        case "--page": startPage = int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--scale": scale = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
        default: break;
    }
}

var themes = new ThemeController(theme);
Element gallery = new GalleryApp(themes) { StartPage = startPage, StartWithDialog = startWithDialog, StartWithMenu = startWithMenu, StartWithPalette = startWithPalette, StartWithSheet = startWithSheet };
var app = new ThemeProvider(themes, rightToLeft ? new Directionality(Radiant.Text.TextDirection.RightToLeft, gallery) : gallery);

if (snapshot is null)
{
    // In a window the theme follows the system's dark mode, accent and accessibility settings,
    // unless colours were chosen on the command line.
    RadiantUI.Run(app with { FollowAppearance = followSystem },
        new UIAppOptions { Title = "Radiant Gallery", Width = 1200, Height = 800, Background = ResolvedTheme.Resolve(theme).Background, Platform = MacPlatform.CreateOrHeadless });
    return;
}

Snapshot(app, snapshot, 1200, height, scale, theme, bench);

static unsafe void Snapshot(Element app, string path, int width, int height, float scale, Theme theme, int bench)
{
    var pixelWidth = (int)(width * scale);
    var pixelHeight = (int)(height * scale);
    using var gpu = new HeadlessGpu();
    using var renderer = new Renderer2D();
    renderer.Initialize(gpu.State, new Camera2D(width, height, Handedness.RightHanded));
    using var target = new OffscreenReadback(gpu, pixelWidth, pixelHeight);
    using var ui = new UIRoot(app);
    ui.Update(new Vector2(width, height));
    // Let entrance animations finish.
    for (var frame = 0; frame < 60; frame++)
    {
        ui.Advance(1 / 60.0);
        ui.Update(new Vector2(width, height));
    }
    if (bench > 0)
    {
        Bench(ui, renderer, target, width, height, pixelWidth, pixelHeight, scale, theme, bench);
    }
    var pixels = target.RenderAndRead(ResolvedTheme.Resolve(theme).Background, pass =>
    {
        renderer.BeginFrame((uint)pixelWidth, (uint)pixelHeight, scale);
        ui.Paint(renderer);
        renderer.EndFrame((Silk.NET.WebGPU.RenderPassEncoder*)pass);
    });
    using var image = SixLabors.ImageSharp.Image.LoadPixelData<Bgra32>(pixels, pixelWidth, pixelHeight);
    image.SaveAsPng(path);
    Console.WriteLine($"wrote {path}");
}

// Times frames as the window loop runs them: advance and update (rebuild, layout), paint
// (recording the draws), end frame (building and uploading the batches), and the rest: the
// offscreen pass's setup, the GPU and the readback, which a window doesn't have in the same form.
static unsafe void Bench(UIRoot ui, Renderer2D renderer, OffscreenReadback target, int width, int height, int pixelWidth, int pixelHeight, float scale, Theme theme, int frames)
{
    var clock = System.Diagnostics.Stopwatch.StartNew();
    double update = 0, paint = 0, end = 0, total = 0;
    for (var frame = 0; frame < frames; frame++)
    {
        var start = clock.Elapsed.TotalMilliseconds;
        ui.Advance(1 / 60.0);
        ui.Update(new Vector2(width, height));
        var updated = clock.Elapsed.TotalMilliseconds;
        double began = 0, painted = 0, ended = 0;
        target.RenderAndRead(ResolvedTheme.Resolve(theme).Background, pass =>
        {
            began = clock.Elapsed.TotalMilliseconds;
            renderer.BeginFrame((uint)pixelWidth, (uint)pixelHeight, scale);
            ui.Paint(renderer);
            painted = clock.Elapsed.TotalMilliseconds;
            renderer.EndFrame((Silk.NET.WebGPU.RenderPassEncoder*)pass);
            ended = clock.Elapsed.TotalMilliseconds;
        });
        var done = clock.Elapsed.TotalMilliseconds;
        update += updated - start;
        paint += painted - began;
        end += ended - painted;
        total += done - start;
    }
    Console.WriteLine($"{frames} frames: update {update / frames:0.00} ms, paint {paint / frames:0.00} ms, end frame {end / frames:0.00} ms, "
        + $"GPU, readback and the rest {(total - update - paint - end) / frames:0.00} ms, total {total / frames:0.00} ms");
}
