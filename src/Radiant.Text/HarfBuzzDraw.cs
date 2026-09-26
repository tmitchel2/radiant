using System;
using System.Runtime.InteropServices;

namespace Radiant.Text;

/// <summary>
/// HarfBuzz's glyph-drawing API, which HarfBuzzSharp does not bind: the native library exports it,
/// so it is called directly. Callbacks are unmanaged function pointers (no delegates, so nothing to
/// keep alive and nothing for trimming or NativeAOT to lose), and the outline being built is passed
/// through as a GCHandle.
/// </summary>
internal static unsafe class HarfBuzzDraw
{
    private const string Library = "libHarfBuzzSharp";

    // One immutable set of draw functions, shared by every call.
    private static readonly IntPtr s_funcs = CreateFuncs();

    /// <summary>The outline of a glyph from a HarfBuzz font, with the font's variations applied.</summary>
    public static GlyphOutline Draw(IntPtr font, uint glyph)
    {
        var builder = new GlyphOutline.Builder();
        var handle = GCHandle.Alloc(builder);
        try
        {
            hb_font_draw_glyph(font, glyph, s_funcs, GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }
        return builder.Build();
    }

    /// <summary>Makes a HarfBuzz font immutable (HarfBuzzSharp does not expose it).</summary>
    public static void MakeImmutable(IntPtr font) => hb_font_make_immutable(font);

    private static IntPtr CreateFuncs()
    {
        var funcs = hb_draw_funcs_create();
        hb_draw_funcs_set_move_to_func(funcs, &MoveTo, IntPtr.Zero, IntPtr.Zero);
        hb_draw_funcs_set_line_to_func(funcs, &LineTo, IntPtr.Zero, IntPtr.Zero);
        hb_draw_funcs_set_quadratic_to_func(funcs, &QuadraticTo, IntPtr.Zero, IntPtr.Zero);
        hb_draw_funcs_set_cubic_to_func(funcs, &CubicTo, IntPtr.Zero, IntPtr.Zero);
        hb_draw_funcs_set_close_path_func(funcs, &ClosePath, IntPtr.Zero, IntPtr.Zero);
        hb_draw_funcs_make_immutable(funcs);
        return funcs;
    }

    private static GlyphOutline.Builder BuilderOf(IntPtr drawData) => (GlyphOutline.Builder)GCHandle.FromIntPtr(drawData).Target!;

    [UnmanagedCallersOnly]
    private static void MoveTo(IntPtr funcs, IntPtr drawData, IntPtr state, float x, float y, IntPtr user) =>
        BuilderOf(drawData).MoveTo(x, y);

    [UnmanagedCallersOnly]
    private static void LineTo(IntPtr funcs, IntPtr drawData, IntPtr state, float x, float y, IntPtr user) =>
        BuilderOf(drawData).LineTo(x, y);

    [UnmanagedCallersOnly]
    private static void QuadraticTo(IntPtr funcs, IntPtr drawData, IntPtr state, float cx, float cy, float x, float y, IntPtr user) =>
        BuilderOf(drawData).QuadTo(cx, cy, x, y);

    [UnmanagedCallersOnly]
    private static void CubicTo(IntPtr funcs, IntPtr drawData, IntPtr state,
        float c1x, float c1y, float c2x, float c2y, float x, float y, IntPtr user) =>
        BuilderOf(drawData).CubicTo(c1x, c1y, c2x, c2y, x, y);

    [UnmanagedCallersOnly]
    private static void ClosePath(IntPtr funcs, IntPtr drawData, IntPtr state, IntPtr user) =>
        BuilderOf(drawData).Close();

    [DllImport(Library)]
    private static extern IntPtr hb_draw_funcs_create();

    [DllImport(Library)]
    private static extern void hb_draw_funcs_make_immutable(IntPtr funcs);

    [DllImport(Library)]
    private static extern void hb_draw_funcs_set_move_to_func(
        IntPtr funcs, delegate* unmanaged<IntPtr, IntPtr, IntPtr, float, float, IntPtr, void> func, IntPtr user, IntPtr destroy);

    [DllImport(Library)]
    private static extern void hb_draw_funcs_set_line_to_func(
        IntPtr funcs, delegate* unmanaged<IntPtr, IntPtr, IntPtr, float, float, IntPtr, void> func, IntPtr user, IntPtr destroy);

    [DllImport(Library)]
    private static extern void hb_draw_funcs_set_quadratic_to_func(
        IntPtr funcs, delegate* unmanaged<IntPtr, IntPtr, IntPtr, float, float, float, float, IntPtr, void> func, IntPtr user, IntPtr destroy);

    [DllImport(Library)]
    private static extern void hb_draw_funcs_set_cubic_to_func(
        IntPtr funcs, delegate* unmanaged<IntPtr, IntPtr, IntPtr, float, float, float, float, float, float, IntPtr, void> func, IntPtr user, IntPtr destroy);

    [DllImport(Library)]
    private static extern void hb_draw_funcs_set_close_path_func(
        IntPtr funcs, delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, void> func, IntPtr user, IntPtr destroy);

    [DllImport(Library)]
    private static extern void hb_font_make_immutable(IntPtr font);

    [DllImport(Library)]
    private static extern void hb_font_draw_glyph(IntPtr font, uint glyph, IntPtr funcs, IntPtr drawData);
}
