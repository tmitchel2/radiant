using System.Numerics;

namespace Radiant.UI;

/// <summary>
/// Standard UI color palette.
/// </summary>
public static class UIColors
{
    public static readonly Vector4 Background = Colors.Neutral800.WithAlpha(0.95f);
    public static readonly Vector4 BackgroundLight = Colors.Neutral700;
    public static readonly Vector4 BackgroundDark = Colors.Neutral900;

    public static readonly Vector4 Primary = Colors.Blue400;
    public static readonly Vector4 PrimaryHover = Colors.Blue300;
    public static readonly Vector4 PrimaryActive = Colors.Blue600;

    public static readonly Vector4 Text = Colors.Neutral100;
    public static readonly Vector4 TextDim = Colors.Neutral400;
    public static readonly Vector4 TextDisabled = Colors.Neutral500;

    public static readonly Vector4 Border = Colors.Neutral600;
    public static readonly Vector4 BorderHover = Colors.Neutral500;

    public static readonly Vector4 SliderTrack = Colors.Neutral700;
    public static readonly Vector4 SliderHandle = Colors.Neutral400;
    public static readonly Vector4 SliderHandleHover = Colors.Neutral300;
    public static readonly Vector4 SliderHandleActive = Colors.Neutral500;
}
