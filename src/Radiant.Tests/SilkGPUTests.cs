using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Old;

namespace Radiant.Tests
{
    [TestClass]
    public class SilkGPUTests : GPUTests
    {
        protected override IGPU CreateGPU()
        {
            return new SilkGPU();
        }
    }
}
