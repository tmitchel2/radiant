namespace Radiant.Host;

/// <summary>
/// A tiny CPU box-average downscaler for drag thumbnails. The source host already has the dragged tab's
/// frame in memory (read each frame for compositing); this shrinks it to an overlay-sized thumbnail that
/// the floating drag overlay paints. It runs only while a drag is in flight and only on a handful of
/// pixels, so the managed cost is negligible — no GPU path is warranted. Box averaging (vs. nearest) keeps
/// the shrunken scene legible.
/// </summary>
internal static class ThumbnailScaler
{
    /// <summary>
    /// Fit <paramref name="srcW"/>×<paramref name="srcH"/> inside a <paramref name="maxW"/>×<paramref name="maxH"/>
    /// box, preserving aspect ratio (each dimension at least 1px).
    /// </summary>
    public static (int W, int H) Fit(int srcW, int srcH, int maxW, int maxH)
    {
        if (srcW <= 0 || srcH <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(srcW), "Source dimensions must be positive.");
        }
        var scale = Math.Min((double)maxW / srcW, (double)maxH / srcH);
        var w = Math.Max(1, (int)Math.Round(srcW * scale));
        var h = Math.Max(1, (int)Math.Round(srcH * scale));
        return (Math.Min(w, maxW), Math.Min(h, maxH));
    }

    /// <summary>
    /// Box-average downscale a tightly packed BGRA8 image (<paramref name="srcW"/>×<paramref name="srcH"/>)
    /// into <paramref name="dst"/> as a tightly packed BGRA8 image (<paramref name="dstW"/>×<paramref name="dstH"/>).
    /// Each destination pixel averages the source pixels in its footprint. <paramref name="dst"/> must hold
    /// at least <c>dstW*dstH*4</c> bytes.
    /// </summary>
    public static void Downscale(ReadOnlySpan<byte> src, int srcW, int srcH, Span<byte> dst, int dstW, int dstH)
    {
        if (srcW <= 0 || srcH <= 0 || dstW <= 0 || dstH <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dstW), "Dimensions must be positive.");
        }
        if (src.Length < srcW * srcH * 4)
        {
            throw new ArgumentException("Source span is smaller than srcW*srcH*4.", nameof(src));
        }
        if (dst.Length < dstW * dstH * 4)
        {
            throw new ArgumentException("Destination span is smaller than dstW*dstH*4.", nameof(dst));
        }

        for (var dy = 0; dy < dstH; dy++)
        {
            var sy0 = dy * srcH / dstH;
            var sy1 = Math.Max(sy0 + 1, (dy + 1) * srcH / dstH);
            for (var dx = 0; dx < dstW; dx++)
            {
                var sx0 = dx * srcW / dstW;
                var sx1 = Math.Max(sx0 + 1, (dx + 1) * srcW / dstW);
                var b = 0;
                var g = 0;
                var r = 0;
                var a = 0;
                var count = 0;
                for (var sy = sy0; sy < sy1; sy++)
                {
                    for (var sx = sx0; sx < sx1; sx++)
                    {
                        var i = ((sy * srcW) + sx) * 4;
                        b += src[i];
                        g += src[i + 1];
                        r += src[i + 2];
                        a += src[i + 3];
                        count++;
                    }
                }
                var o = ((dy * dstW) + dx) * 4;
                dst[o] = (byte)(b / count);
                dst[o + 1] = (byte)(g / count);
                dst[o + 2] = (byte)(r / count);
                dst[o + 3] = (byte)(a / count);
            }
        }
    }
}
