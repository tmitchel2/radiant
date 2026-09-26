using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.ColorSystem.Tests;

/// <summary>Upstream's blend_test.ts.</summary>
[TestClass]
public class BlendTests
{
    private const uint Red = 0xFFFF0000;
    private const uint Blue = 0xFF0000FF;
    private const uint Green = 0xFF00FF00;
    private const uint Yellow = 0xFFFFFF00;

    [TestMethod]
    [DataRow(Red, Blue, 0xFFFB0057u)]
    [DataRow(Red, Green, 0xFFD85600u)]
    [DataRow(Red, Yellow, 0xFFD85600u)]
    [DataRow(Blue, Green, 0xFF0047A3u)]
    [DataRow(Blue, Red, 0xFF5700DCu)]
    [DataRow(Blue, Yellow, 0xFF0047A3u)]
    [DataRow(Green, Blue, 0xFF00FC94u)]
    [DataRow(Green, Red, 0xFFB1F000u)]
    [DataRow(Green, Yellow, 0xFFB1F000u)]
    [DataRow(Yellow, Blue, 0xFFEBFFBAu)]
    [DataRow(Yellow, Green, 0xFFEBFFBAu)]
    [DataRow(Yellow, Red, 0xFFFFF6E3u)]
    public void HarmonizeShiftsTheHueTowardsTheSourceColour(uint design, uint source, uint expected)
    {
        var answer = Blend.Harmonize(unchecked((int)design), unchecked((int)source));

        Assert.AreEqual(Fixture.Hex(unchecked((int)expected)), Fixture.Hex(answer));
    }
}
