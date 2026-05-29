using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Animation;

namespace Radiant.Tests.Animation;

[TestClass]
public class SmoothDampTests
{
    [TestMethod]
    public void ConvergesToTarget()
    {
        var value = 0f;
        var velocity = 0f;
        for (var i = 0; i < 600; i++)
            value = SmoothDamp.Step(value, 100f, ref velocity, 0.25f, 1f / 60f);

        Assert.AreEqual(100f, value, 0.01f);
        Assert.AreEqual(0f, velocity, 0.5f);
    }

    [TestMethod]
    public void DoesNotOvershoot()
    {
        var value = 0f;
        var velocity = 0f;
        var maxSeen = 0f;
        for (var i = 0; i < 600; i++)
        {
            value = SmoothDamp.Step(value, 100f, ref velocity, 0.25f, 1f / 60f);
            maxSeen = MathF.Max(maxSeen, value);
        }

        // Critically damped: approaches from below, never exceeds the target.
        Assert.IsTrue(maxSeen <= 100f + 0.001f, $"overshot to {maxSeen}");
    }

    [TestMethod]
    public void MaxSpeedClampsTravelPerStep()
    {
        var value = 0f;
        var velocity = 0f;
        // Tiny smoothTime would otherwise jump far in one step; maxSpeed caps it.
        var next = SmoothDamp.Step(value, 1000f, ref velocity, 0.05f, maxSpeed: 10f, deltaTime: 1f / 60f);

        Assert.IsTrue(next < value + 10f, "step should respect the max-speed clamp");
    }

    [TestMethod]
    public void RespondsFromArbitraryStartingVelocity()
    {
        // A spring handed an inbound velocity (e.g. momentum hand-off) still settles.
        var value = 50f;
        var velocity = 800f;
        for (var i = 0; i < 600; i++)
            value = SmoothDamp.Step(value, 50f, ref velocity, 0.2f, 1f / 60f);

        Assert.AreEqual(50f, value, 0.05f);
    }
}
