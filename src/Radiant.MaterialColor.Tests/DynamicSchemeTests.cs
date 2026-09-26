using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.MaterialColor.Tests;

/// <summary>Upstream's dynamic_scheme_test.ts, plus the behaviour this port adds.</summary>
[TestClass]
public class DynamicSchemeTests
{
    private static readonly Hct s_source = Hct.From(43, 16, 16);

    [TestMethod]
    public void NoBreakpointsMeansNoRotation()
    {
        Assert.AreEqual(43, DynamicScheme.GetRotatedHue(s_source, [], []), 0.4);
    }

    [TestMethod]
    public void OneBreakpointMeansNoRotation()
    {
        Assert.AreEqual(43, DynamicScheme.GetRotatedHue(s_source, [0], [0]), 0.4);
    }

    [TestMethod]
    public void MismatchedLengthsUseTheShorter()
    {
        Assert.AreEqual(43, DynamicScheme.GetRotatedHue(s_source, [0], [0, 1]), 0.4);
    }

    [TestMethod]
    public void AHueOnABreakpointTakesTheRotationAboveIt()
    {
        Assert.AreEqual(43 + 15, DynamicScheme.GetRotatedHue(s_source, [0, 42, 360], [0, 15, 0]), 0.4);
    }

    [TestMethod]
    public void RotationsPast360DegreesWrap()
    {
        Assert.AreEqual(163, DynamicScheme.GetRotatedHue(s_source, [0, 42, 360], [0, 480, 0]), 0.4);
    }

    [TestMethod]
    public void AnEmptySourceListIsRefused()
    {
        Assert.ThrowsException<ArgumentException>(() => new SchemeTonalSpot(Array.Empty<Hct>(), false, 0));
    }

    [TestMethod]
    public void CmfRefusesSpecsOtherThan2026()
    {
        Assert.ThrowsException<ArgumentException>(() => new SchemeCmf(s_source, false, 0, SpecVersion.Spec2025));
    }

    [TestMethod]
    public void VariantsThe2025SpecDoesNotCoverFallBackTo2021()
    {
        Assert.AreEqual(SpecVersion.Spec2021, new SchemeFidelity(s_source, false, 0, SpecVersion.Spec2025).SpecVersion);
        Assert.AreEqual(SpecVersion.Spec2025, new SchemeTonalSpot(s_source, false, 0, SpecVersion.Spec2026).SpecVersion);
        Assert.AreEqual(SpecVersion.Spec2026, new SchemeCmf(s_source, false, 0).SpecVersion);
    }

    [TestMethod]
    public void ASchemeDescribesItself()
    {
        var text = new SchemeTonalSpot(Hct.FromInt(unchecked((int)0xFF6750A4)), true, 0.5, SpecVersion.Spec2025).ToString();

        StringAssert.StartsWith(text, "Scheme: variant=TonalSpot, mode=dark, platform=Phone, contrastLevel=0.5, seed=HCT(");
        StringAssert.EndsWith(text, "specVersion=Spec2025");
    }

    [TestMethod]
    public void ResolvingTheSameRoleTwiceReturnsTheCachedColour()
    {
        var scheme = new SchemeVibrant(s_source, false, 0);

        Assert.AreSame(scheme.GetHct(MaterialDynamicColors.Primary()), scheme.GetHct(MaterialDynamicColors.Primary()));
    }

    [TestMethod]
    public void SchemesResolveTheSameOnManyThreadsAsOnOne()
    {
        var scheme = new SchemeExpressive(s_source, true, 0.5, SpecVersion.Spec2025);
        var expected = new SchemeExpressive(s_source, true, 0.5, SpecVersion.Spec2025);
        var roles = MaterialDynamicColors.AllColors;

        System.Threading.Tasks.Parallel.For(0, 64, i =>
        {
            var role = roles[i % roles.Count];
            Assert.AreEqual(expected.GetArgb(role), scheme.GetArgb(role), role.Name);
        });
    }
}
