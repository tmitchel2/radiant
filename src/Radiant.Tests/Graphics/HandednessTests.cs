using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics;

namespace Radiant.Tests.Graphics
{
    [TestClass]
    public class HandednessTests
    {
        [TestMethod]
        public void LeftHanded_HasValue()
        {
            var handedness = Handedness.LeftHanded;
            Assert.AreEqual(Handedness.LeftHanded, handedness);
        }

        [TestMethod]
        public void RightHanded_HasValue()
        {
            var handedness = Handedness.RightHanded;
            Assert.AreEqual(Handedness.RightHanded, handedness);
        }

        [TestMethod]
        public void Default_IsNotAValidValue()
        {
            var defaultValue = default(Handedness);
            Assert.AreNotEqual(Handedness.LeftHanded, defaultValue);
            Assert.AreNotEqual(Handedness.RightHanded, defaultValue);
        }

        [TestMethod]
        public void LeftHanded_HasExpectedIntValue()
        {
            Assert.AreEqual(1, (int)Handedness.LeftHanded);
        }

        [TestMethod]
        public void RightHanded_HasExpectedIntValue()
        {
            Assert.AreEqual(2, (int)Handedness.RightHanded);
        }
    }
}
