using Microsoft.VisualStudio.TestTools.UnitTesting;

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
