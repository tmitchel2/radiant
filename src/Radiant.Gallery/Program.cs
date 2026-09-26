using System;
using System.Numerics;
using Radiant.ColorSystem;
using Radiant.Gallery;
using Radiant.Graphics;
using Radiant.Graphics2D;
using Radiant.Theming;
using Radiant.UI.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

// radiant-gallery                         opens the gallery in a window
// radiant-gallery --snapshot out.png [--dark] [--seed #rrggbb] [--variant Vibrant] [--scale 2] [--height 1400] [--dialog] [--menu]
//                                         renders it offscreen to a PNG instead
var theme = new Theme();
string? snapshot = null;
var startWithDialog = false;
var startWithMenu = false;
var scale = 1f;
var height = 640;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--snapshot": snapshot = args[++i]; break;
        case "--dark": theme = theme with { Colors = theme.Colors with { IsDark = true } }; break;
        case "--seed": theme = theme with { Colors = theme.Colors with { Seed = Radiant.Graphics2D.Color.Parse(args[++i]) } }; break;
        case "--variant": theme = theme with { Colors = theme.Colors with { Variant = Enum.Parse<Variant>(args[++i]) } }; break;
        case "--height": height = int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--dialog": startWithDialog = true; break;
        case "--menu": startWithMenu = true; break;
        case "--scale": scale = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
        default: break;
    }
}

var themes = new ThemeController(theme);
var app = new ThemeProvider(themes, new VerticalSlice(themes) { StartWithDialog = startWithDialog, StartWithMenu = startWithMenu });

if (snapshot is null)
{
    RadiantUI.Run(app, new UIAppOptions { Title = "Radiant Gallery", Width = 900, Height = 720, Background = ResolvedTheme.Resolve(theme).Background });
    return;
}

Snapshot(app, snapshot, 900, height, scale, theme);

static unsafe void Snapshot(Element app, string path, int width, int height, float scale, Theme theme)
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
    var pixels = target.RenderAndRead(ResolvedTheme.Resolve(theme).Background, pass =>
    {
        renderer.BeginFrame((uint)pixelWidth, (uint)pixelHeight, scale);
        ui.Paint(renderer);
        renderer.EndFrame((Silk.NET.WebGPU.RenderPassEncoder*)pass);
    });
    using var image = Image.LoadPixelData<Bgra32>(pixels, pixelWidth, pixelHeight);
    image.SaveAsPng(path);
    Console.WriteLine($"wrote {path}");
}
