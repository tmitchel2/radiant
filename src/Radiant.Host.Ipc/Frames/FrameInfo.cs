namespace Radiant.Host.Ipc.Frames;

/// <summary>
/// Metadata describing a frame returned by <see cref="SharedFrameBuffer.TryRead"/>.
/// </summary>
/// <param name="Width">Frame width in pixels.</param>
/// <param name="Height">Frame height in pixels.</param>
/// <param name="Format">Pixel layout of the frame bytes.</param>
/// <param name="FrameIndex">Monotonic count of frames published by the writer at the time this frame was produced.</param>
public readonly record struct FrameInfo(int Width, int Height, FrameFormat Format, long FrameIndex)
{
    /// <summary>Tightly packed byte length of the frame (no row padding): width * height * 4.</summary>
    public int ByteLength => Width * Height * 4;
}
