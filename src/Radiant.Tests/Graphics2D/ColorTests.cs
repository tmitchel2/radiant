using System;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;

namespace Radiant.Tests.Graphics2D;

[TestClass]
public class ColorTests
{
    private const float Tolerance = 1e-6f;

    [TestMethod]
    public void EveryEncodedByteSurvivesARoundTripThroughLinear()
    {
        for (var i = 0; i < 256; i++)
        {
            var linear = SrgbTransfer.ToLinear((byte)i);
            Assert.AreEqual(i, SrgbTransfer.ToSrgbByte(linear), $"byte {i}");
        }
    }

    [TestMethod]
    public void LinearMidGreyEncodesAsSrgb188()
    {
        // The canonical check that blending is in linear light: half-intensity light is sRGB 188, not 128.
        Assert.AreEqual(188, SrgbTransfer.ToSrgbByte(0.5f));
        Assert.AreEqual(0.2158605f, SrgbTransfer.ToLinear((byte)128), 1e-6f);
    }

    [TestMethod]
    public void ByteAndFloatDecodingAgree()
    {
        for (var i = 0; i < 256; i++)
        {
            Assert.AreEqual(SrgbTransfer.ToLinear(i / 255f), SrgbTransfer.ToLinear((byte)i), Tolerance);
        }
    }

    [TestMethod]
    public void ParsesSixDigitHexIntoLinearLight()
    {
        var color = Color.Parse("#3b82f6");

        Assert.AreEqual(SrgbTransfer.ToLinear((byte)0x3b), color.R, Tolerance);
        Assert.AreEqual(SrgbTransfer.ToLinear((byte)0x82), color.G, Tolerance);
        Assert.AreEqual(SrgbTransfer.ToLinear((byte)0xf6), color.B, Tolerance);
        Assert.AreEqual(1f, color.A);
    }

    [TestMethod]
    public void ParsesEightDigitHexWithAlphaLastAsInCss()
    {
        var color = Color.Parse("#ff000080");

        Assert.AreEqual(1f, color.R, Tolerance);
        Assert.AreEqual(0f, color.G, Tolerance);
        Assert.AreEqual(128 / 255f, color.A, Tolerance);
    }

    [TestMethod]
    public void ShortHexRepeatsEachDigit()
    {
        Assert.AreEqual(Color.Parse("#ff8800"), Color.Parse("#f80"));
        Assert.AreEqual(Color.Parse("#ff880044"), Color.Parse("#f804"));
    }

    [TestMethod]
    public void HashIsOptional()
    {
        Assert.AreEqual(Color.Parse("#3b82f6"), Color.Parse("3b82f6"));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("#")]
    [DataRow("#12")]
    [DataRow("#12345")]
    [DataRow("#1234567")]
    [DataRow("#123456789")]
    [DataRow("#gggggg")]
    [DataRow(" #123456")]
    [DataRow("#-12345")]
    public void RejectsMalformedHex(string hex)
    {
        Assert.IsFalse(Color.TryParse(hex, out _));
        Assert.ThrowsException<FormatException>(() => Color.Parse(hex));
    }

    [TestMethod]
    public void ArgbRoundTripsExactly()
    {
        uint[] samples = [0xFF3B82F6, 0x80FF0000, 0x00000000, 0xFFFFFFFF, 0x12345678];
        foreach (var argb in samples)
        {
            Assert.AreEqual(argb, Color.FromArgb(argb).ToArgb(), $"0x{argb:X8}");
        }
    }

    [TestMethod]
    public void SignedArgbFromAColourLibraryMatchesUnsigned()
    {
        // Radiant.ColorSystem hands back opaque colours as negative ints.
        var signed = unchecked((int)0xFF6750A4);

        Assert.AreEqual(Color.FromArgb(0xFF6750A4), Color.FromArgb(signed));
    }

    [TestMethod]
    public void ArgbPacksAlphaFirstWhileHexPutsItLast()
    {
        Assert.AreEqual(Color.Parse("#11223344"), Color.FromArgb(0x44112233));
    }

    [TestMethod]
    public void ToStringIsCssHexWithAlpha()
    {
        Assert.AreEqual("#3b82f6ff", Color.Parse("#3b82f6").ToString());
        Assert.AreEqual("#ff000080", Color.FromArgb(0x80FF0000).ToString());
    }

    [TestMethod]
    public void ToArgbClampsOutOfRangeComponents()
    {
        Assert.AreEqual(0xFFFF0000u, new Color(4f, -1f, -0.5f, 2f).ToArgb());
    }

    [TestMethod]
    public void WithAlphaKeepsTheColour()
    {
        var faded = Color.Parse("#3b82f6").WithAlpha(0.25f);

        Assert.AreEqual(Color.Parse("#3b82f6") with { A = 0.25f }, faded);
    }

    [TestMethod]
    public void PremultipliedScalesRgbByAlpha()
    {
        var premultiplied = new Color(1f, 0.5f, 0.25f, 0.5f).ToPremultiplied();

        Assert.AreEqual(new Vector4(0.5f, 0.25f, 0.125f, 0.5f), premultiplied);
    }

    [TestMethod]
    public void ConvertsImplicitlyToAStraightAlphaVector()
    {
        Vector4 vector = new Color(0.1f, 0.2f, 0.3f, 0.4f);

        Assert.AreEqual(new Vector4(0.1f, 0.2f, 0.3f, 0.4f), vector);
    }

    [TestMethod]
    public void FadingToTransparentKeepsTheHue()
    {
        var red = new Color(1f, 0f, 0f);

        var halfway = Color.Lerp(red, Color.Transparent, 0.5f);

        // A straight-alpha lerp would give (0.5, 0, 0, 0.5): half-faded AND darkened.
        Assert.AreEqual(1f, halfway.R, Tolerance);
        Assert.AreEqual(0.5f, halfway.A, Tolerance);
    }

    [TestMethod]
    public void LerpBetweenOpaqueColoursIsLinear()
    {
        var mid = Color.Lerp(Color.Black, Color.White, 0.5f);

        Assert.AreEqual(new Color(0.5f, 0.5f, 0.5f), mid);
    }

    [TestMethod]
    public void LerpOfTwoTransparentColoursIsTransparent()
    {
        Assert.AreEqual(Color.Transparent, Color.Lerp(Color.Transparent, new Color(1f, 1f, 1f, 0f), 0.5f));
    }

    [TestMethod]
    public void PaletteHexAgreesWithColorParse()
    {
        var parsed = Color.Parse("#3b82f6");

        Assert.AreEqual(new Vector3(parsed.R, parsed.G, parsed.B), Colors.Blue500);
    }
}
