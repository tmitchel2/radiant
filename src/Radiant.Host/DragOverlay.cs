using System.Numerics;
using Radiant.Graphics;
using Radiant.Graphics2D;
using Radiant.Host.AgentControlProtocol;
using Radiant.Host.Ipc;
using Radiant.Host.Ipc.Frames;
using Silk.NET.WebGPU;

namespace Radiant.Host;

/// <summary>
/// The floating drag overlay: a small, borderless, transparent, always-on-top, click-through window
/// (the "drag layer") that follows the global cursor and paints the dragged tab's ghost while a drag is
/// in flight — giving Chrome-style visual continuity as a tab crosses from one host window into another.
///
/// <para>It is a separate process (the radiant run loop is single-window/blocking, so a host can't open a
/// second window in-process). It is purely cosmetic: the source host owns pointer capture for a held
/// drag and resolves the drop, so this window stays click-through and never intercepts events. It polls
/// the <see cref="DragSession"/> control file for the cursor + label; it deliberately does NOT open the
/// dragged tab's <see cref="Radiant.Host.Ipc.Frames.SharedFrameBuffer"/> (that is single-consumer — the
/// source host is reading it as the active tab; a second reader would starve it), so the ghost is a
/// labelled chip. A live-frame thumbnail is a deferred fidelity item (see the roadmap).</para>
/// </summary>
internal sealed class DragOverlay : IDisposable
{
    /// <summary>Instance name the overlay registers under (so hosts can detect/spawn exactly one).</summary>
    public static string OverlayInstanceName => RadiantAppIdentity.Current.OverlayInstanceName;

    // Sized to hold the host-published thumbnail (ThumbMaxWidth/Height in LiveHost) with a little margin.
    private const int Width = 192;
    private const int Height = 128;
    private const double ExitGraceSeconds = 2.0;

    private MsdfFont? _font;
    private string _label = "";
    private bool _visible;
    private double _idleSeconds;

    // Live thumbnail of the dragged tab, read from the host-published buffer (a clean second SPSC channel:
    // the host writes, this overlay reads — it never opens the renderer's own single-consumer frame buffer).
    // The reader is opened lazily per drag and dropped when the drag ends; until a frame is read the overlay
    // falls back to the labelled chip.
    private string _thumbPath = "";
    private SharedFrameBuffer? _thumbReader;
    private string? _thumbReaderPath;
    private byte[]? _thumbScratch;
    private Texture2D? _thumbTexture;
    private int _thumbW;
    private int _thumbH;
    private bool _hasThumb;

    public void Run()
    {
        Register();
        Console.WriteLine($"{RadiantAppIdentity.Current.Name} drag-overlay starting (transparent click-through ghost layer).");
        try
        {
            using var app = new RadiantApplication();
            var style = new RadiantWindowStyle
            {
                Decorated = false,
                Transparent = true,
                TopMost = true,
                MousePassthrough = true,
                Visible = false,
                // Show without stealing key-window focus: otherwise revealing the overlay mid-drag sends
                // the host a spurious mouse-up that ends the drag (the real root cause, not passthrough).
                FocusOnShow = false,
            };
            app.Run(
                title: $"{RadiantAppIdentity.Current.Name} Drag",
                width: Width,
                height: Height,
                handedness: Handedness.RightHanded,
                renderCallback: Render,
                updateCallback: dt => Update(app, dt),
                backgroundColor: new Vector4(0f, 0f, 0f, 0f),
                style: style);
        }
        finally
        {
            InstanceRegistry.Deregister(OverlayInstanceName);
        }
    }

    private void Update(RadiantApplication app, double dt)
    {
        // Drop out of the Dock: GLFW makes the overlay a Regular app while creating its window, which
        // gives this cosmetic click-through ghost a generic Dock tile. Switch to Accessory once the
        // window/NSApplication exists (idempotent — only the first call does work).
        MacDockIcon.TrySetAccessoryPolicy();

        var session = DragSession.Read();
        // The floating thumbnail hides when the cursor is over a strip (the strip-owning host shows an
        // in-strip placeholder) and once a live tear-off has fired (the real following window is the
        // feedback) — treat OverStrip/TornOff like inactive for visibility.
        if (session is { Active: true, OverStrip: false, TornOff: false })
        {
            _idleSeconds = 0;
            _label = session.Label;
            _thumbPath = session.ThumbnailPath;
            var (x, y) = DragOverlayPlacement.WindowTopLeft(session.CursorX, session.CursorY, Width, Height);
            app.MoveWindow(x, y);
            if (!_visible)
            {
                app.SetVisible(true);
                _visible = true;
            }
            return;
        }

        if (_visible)
        {
            app.SetVisible(false);
            _visible = false;
            // Drop the per-drag thumbnail reader so the next drag reopens a fresh buffer.
            CloseThumbReader();
            _thumbPath = "";
        }

        // Exit once no compositing host remains (after a short grace so we don't race host startup).
        _idleSeconds += dt;
        if (_idleSeconds > ExitGraceSeconds && !AnyHostAlive())
        {
            app.Close();
        }
    }

    private void Render(Renderer2D renderer)
    {
        _font ??= LoadFont(renderer);
        if (!_visible)
        {
            return;
        }

        // Prefer a live thumbnail of the dragged tab; fall back to the labelled chip until one is readable.
        if (TryUpdateThumbnail(renderer) && _thumbTexture is { } tex)
        {
            // Centre the aspect-preserved thumbnail in the overlay window.
            var x = (Width - _thumbW) / 2f;
            var y = (Height - _thumbH) / 2f;
            renderer.DrawImage(tex, x, y, _thumbW, _thumbH);
            return;
        }

        HostCompositor.DrawDragGhost(renderer, new Vector2(Width / 2f, Height / 2f), _font, _label);
    }

    /// <summary>
    /// Open (lazily) the host-published thumbnail buffer and pull the latest frame into <see cref="_thumbTexture"/>.
    /// Returns true once at least one frame has been read for the current drag (so a transient "no new frame"
    /// keeps showing the last thumbnail rather than flickering back to the chip). False until the first read,
    /// or if the buffer is missing/incompatible (chip fallback).
    /// </summary>
    private bool TryUpdateThumbnail(Renderer2D renderer)
    {
        if (string.IsNullOrEmpty(_thumbPath))
        {
            return false;
        }
        if (_thumbReader is null || !string.Equals(_thumbReaderPath, _thumbPath, StringComparison.Ordinal))
        {
            CloseThumbReader();
            if (!File.Exists(_thumbPath))
            {
                return false;
            }
            try
            {
                _thumbReader = SharedFrameBuffer.OpenReader(_thumbPath);
                _thumbReaderPath = _thumbPath;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException)
            {
                CloseThumbReader();
                return false; // buffer mid-(re)creation or incompatible — retry next frame
            }
        }

        _thumbScratch ??= new byte[Width * Height * 4];
        if (_thumbReader.TryRead(_thumbScratch, out var info))
        {
            if (_thumbTexture is null || _thumbTexture.Width != info.Width || _thumbTexture.Height != info.Height)
            {
                _thumbTexture?.Dispose();
                // The published bytes are the renderer's sRGB-encoded frame (see OffscreenPresentationSurface),
                // so sample as sRGB to avoid a double encode — same reasoning as the host's tab texture.
                _thumbTexture = Texture2D.Create(renderer, info.Width, info.Height, TextureFormat.Bgra8UnormSrgb);
            }
            _thumbTexture.Update(_thumbScratch.AsSpan(0, info.Width * info.Height * 4));
            _thumbW = info.Width;
            _thumbH = info.Height;
            _hasThumb = true;
        }
        return _hasThumb;
    }

    private void CloseThumbReader()
    {
        _thumbReader?.Dispose();
        _thumbReader = null;
        _thumbReaderPath = null;
        _thumbTexture?.Dispose();
        _thumbTexture = null;
        _hasThumb = false;
    }

    private static bool AnyHostAlive()
    {
        foreach (var info in InstanceRegistry.ListInstances())
        {
            if (Array.IndexOf(info.Capabilities, "tab") >= 0)
            {
                return true;
            }
        }
        return false;
    }

    private static MsdfFont LoadFont(Renderer2D renderer)
    {
        var font = MsdfFont.LoadEmbedded(EmbeddedFonts.Default);
        renderer.RegisterMsdfFont(font);
        return font;
    }

    private static void Register()
    {
        InstanceRegistry.Register(new InstanceInfo
        {
            Name = OverlayInstanceName,
            Pid = Environment.ProcessId,
            StartTime = DateTime.UtcNow.ToString("o"),
            WorkingDirectory = Directory.GetCurrentDirectory(),
            Capabilities = ["drag-overlay"],
            ProtocolVersion = TabProtocol.Version,
        });
    }

    public void Dispose()
    {
        CloseThumbReader();
        _font?.Dispose();
        _font = null;
    }
}
