using System;
using System.Numerics;
using Radiant.ColorSystem;
using Radiant.Gallery;
using Radiant.Graphics;
using Radiant.Graphics2D;
using Radiant.Platform.MacOS;
using Radiant.Theming;
using Radiant.UI.Automation;
using Radiant.UI.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

// radiant-gallery                         opens the gallery in a window, following the system appearance
// radiant-gallery --agent [--headless]    the same, driven by an agent or a test (RADIANT_AGENT=1 does the same)
// radiant-gallery --snapshot out.png [--theme Tonal|Quartz|Linen] [--dark] [--seed #rrggbb] [--variant Vibrant] [--scale 2] [--height 1400] [--page 0-14] [--dialog] [--menu] [--palette] [--sheet] [--rtl] [--bench N [--bench-theme]]
//                                         renders it offscreen to a PNG instead
var theme = ThemePresets.Tonal;
string? snapshot = null;
var startWithDialog = false;
var startWithPalette = false;
var startWithSheet = false;
var bench = 0;
var benchTheme = false;
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
        case "--theme": theme = theme.WithStyle(ThemePresets.Find(args[++i]) ?? throw new ArgumentException($"No theme called {args[i]}; there's {string.Join(", ", System.Linq.Enumerable.Select(ThemePresets.All, t => t.Name))}")); break;
        case "--dark": theme = theme with { Colors = theme.Colors with { IsDark = true } }; followSystem = false; break;
        case "--seed": theme = theme with { Colors = theme.Colors with { Seed = Radiant.Graphics2D.Color.Parse(args[++i]) } }; followSystem = false; break;
        case "--variant": theme = theme with { Colors = theme.Colors with { Variant = Enum.Parse<Variant>(args[++i]) } }; break;
        case "--height": height = int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--dialog": startWithDialog = true; break;
        case "--palette": startWithPalette = true; break;
        case "--rtl": rightToLeft = true; break;
        case "--sheet": startWithSheet = true; break;
        case "--bench": bench = int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
        // Times frames while the theme animates between light and dark the whole time.
        case "--bench-theme": benchTheme = true; break;
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
    // Under automation (RADIANT_AGENT=1 or --agent) it also answers agents and tests, and may run headless.
    var options = RadiantAutomation.Configure(
        new UIAppOptions { Title = "Radiant Gallery", Width = 1200, Height = 800, Background = ResolvedTheme.Resolve(theme).Background, Platform = MacPlatform.CreateOrHeadless },
        args, "radiant-gallery");
    RadiantUI.Run(app with { FollowAppearance = followSystem }, options);
    return;
}

Snapshot(app, snapshot, 1200, height, scale, theme, bench, benchTheme ? themes : null);

static unsafe void Snapshot(Element app, string path, int width, int height, float scale, Theme theme, int bench, ThemeController? animate)
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
        Bench(ui, renderer, target, width, height, pixelWidth, pixelHeight, scale, theme, bench, animate);
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
// Update times are also given as percentiles over the frames after the first second, once the
// JIT has optimised what runs every frame, with the heap allocated per frame.
static unsafe void Bench(UIRoot ui, Renderer2D renderer, OffscreenReadback target, int width, int height, int pixelWidth, int pixelHeight, float scale, Theme theme, int frames, ThemeController? animate)
{
    const int warmUp = 60;
    var clock = System.Diagnostics.Stopwatch.StartNew();
    double update = 0, paint = 0, end = 0, total = 0;
    var updates = new System.Collections.Generic.List<double>();
    // One buffer for every frame's pixels: a fresh 3 MB array a frame would time the collector, not the UI.
    var pixels = new byte[pixelWidth * pixelHeight * 4];
    var background = ResolvedTheme.Resolve(theme).Background;
    var allocated = GC.GetAllocatedBytesForCurrentThread();
    for (var frame = 0; frame < frames; frame++)
    {
        // A 300 ms transition to the other appearance, started again as each ends (18 frames at 60 Hz).
        if (animate is not null && frame % 18 == 0)
        {
            var current = animate.Theme;
            animate.Set(current with { Colors = current.Colors with { IsDark = !current.Colors.IsDark } }, TimeSpan.FromMilliseconds(300));
        }
        var start = clock.Elapsed.TotalMilliseconds;
        ui.Advance(1 / 60.0);
        ui.Update(new Vector2(width, height));
        var updated = clock.Elapsed.TotalMilliseconds;
        double began = 0, painted = 0, ended = 0;
        target.RenderAndRead(background, pass =>
        {
            began = clock.Elapsed.TotalMilliseconds;
            renderer.BeginFrame((uint)pixelWidth, (uint)pixelHeight, scale);
            ui.Paint(renderer);
            painted = clock.Elapsed.TotalMilliseconds;
            renderer.EndFrame((Silk.NET.WebGPU.RenderPassEncoder*)pass);
            ended = clock.Elapsed.TotalMilliseconds;
        }, pixels);
        var done = clock.Elapsed.TotalMilliseconds;
        update += updated - start;
        paint += painted - began;
        end += ended - painted;
        total += done - start;
        if (frame >= warmUp)
        {
            updates.Add(updated - start);
        }
    }
    var perFrame = (GC.GetAllocatedBytesForCurrentThread() - allocated) / frames / 1024;
    Console.WriteLine($"{frames} frames: update {update / frames:0.00} ms, paint {paint / frames:0.00} ms, end frame {end / frames:0.00} ms, "
        + $"GPU, readback and the rest {(total - update - paint - end) / frames:0.00} ms, total {total / frames:0.00} ms; {perFrame} KB allocated a frame");
    if (updates.Count > 0)
    {
        updates.Sort();
        double At(double p) => updates[(int)Math.Min(updates.Count - 1, updates.Count * p)];
        Console.WriteLine($"update after warm-up: median {At(0.5):0.00} ms, 95th {At(0.95):0.00} ms, 99th {At(0.99):0.00} ms, slowest {updates[^1]:0.00} ms");
    }
}
