using System.Numerics;

namespace Radiant.UI;

/// <summary>
/// Standard UI color palette.
/// </summary>
public static class UIColors
{
    public static readonly Vector4 Background = new(0.15f, 0.15f, 0.15f, 0.95f);
    public static readonly Vector4 BackgroundLight = new(0.22f, 0.22f, 0.22f, 1f);
    public static readonly Vector4 BackgroundDark = new(0.1f, 0.1f, 0.1f, 1f);

    public static readonly Vector4 Primary = new(0.26f, 0.59f, 0.98f, 1f);
    public static readonly Vector4 PrimaryHover = new(0.36f, 0.69f, 1f, 1f);
    public static readonly Vector4 PrimaryActive = new(0.16f, 0.49f, 0.88f, 1f);

    public static readonly Vector4 Text = new(0.95f, 0.95f, 0.95f, 1f);
    public static readonly Vector4 TextDim = new(0.7f, 0.7f, 0.7f, 1f);
    public static readonly Vector4 TextDisabled = new(0.5f, 0.5f, 0.5f, 1f);

    public static readonly Vector4 Border = new(0.4f, 0.4f, 0.4f, 1f);
    public static readonly Vector4 BorderHover = new(0.5f, 0.5f, 0.5f, 1f);

    public static readonly Vector4 SliderTrack = new(0.3f, 0.3f, 0.3f, 1f);
    public static readonly Vector4 SliderHandle = new(0.7f, 0.7f, 0.7f, 1f);
    public static readonly Vector4 SliderHandleHover = new(0.85f, 0.85f, 0.85f, 1f);
    public static readonly Vector4 SliderHandleActive = new(0.6f, 0.6f, 0.6f, 1f);
}
