using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Animation;

namespace Radiant.Tests.Animation;

[TestClass]
public class DecayTests
{
    [TestMethod]
    public void FactorIsFrameRateIndependent()
    {
        // Decaying once over 32 ms must equal decaying twice over 16 ms.
        var oneBigStep = Decay.Factor(Decay.NormalRatePerMs, 0.032);
        var twoSmallSteps =
            Decay.Factor(Decay.NormalRatePerMs, 0.016) *
            Decay.Factor(Decay.NormalRatePerMs, 0.016);

        Assert.AreEqual(oneBigStep, twoSmallSteps, 1e-6f);
    }

    [TestMethod]
    public void ApplyDecaysVelocityTowardZero()
    {
        var v = 1000f;
        for (var i = 0; i < 200; i++)
            Decay.Apply(ref v, Decay.NormalRatePerMs, 0.016);

        Assert.IsTrue(v > 0f, "normal decay should not flip sign");
        Assert.IsTrue(v < 600f, "velocity should have measurably decayed");
    }

    [TestMethod]
    public void FastDecaysQuickerThanNormal()
    {
        var vn = 1000f;
        var vf = 1000f;
        for (var i = 0; i < 60; i++)
        {
            Decay.Apply(ref vn, Decay.NormalRatePerMs, 0.016);
            Decay.Apply(ref vf, Decay.FastRatePerMs, 0.016);
        }

        Assert.IsTrue(vf < vn, "fast preset should bleed velocity faster than normal");
    }

    [TestMethod]
    public void ProjectedDistanceMatchesIntegratedTravel()
    {
        const float v0 = 1200f; // units/sec
        const float rate = Decay.NormalRatePerMs;

        // Numerically integrate decaying velocity to rest with fine steps.
        var v = v0;
        var travelled = 0f;
        const double dt = 0.001;
        for (var i = 0; i < 20000 && MathF.Abs(v) > 0.01f; i++)
        {
            travelled += v * (float)dt;
            Decay.Apply(ref v, rate, dt);
        }

        Assert.AreEqual(Decay.ProjectedDistance(v0, rate), travelled, travelled * 0.01f);
    }
}
