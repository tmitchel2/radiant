using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.ColorSystem.Tests;

/// <summary>The dislike analyzer against upstream's outputs.</summary>
[TestClass]
public class DislikeFixtureTests
{
    [TestMethod]
    public void DislikedColoursAndTheirFixesMatchUpstream()
    {
        var mismatches = new Mismatches();
        foreach (var row in Fixture.Load("dislike").EnumerateArray())
        {
            var argb = Fixture.Argb(row[0]);
            var hct = Hct.FromInt(argb);
            var label = Fixture.Hex(argb);
            mismatches.Bool(row[1].GetBoolean(), DislikeAnalyzer.IsDisliked(hct), label + " disliked");
            mismatches.Colour(Fixture.Argb(row[2]), DislikeAnalyzer.FixIfDisliked(hct).ToInt(), label + " fixed");
        }
        mismatches.AssertNone();
    }
}
