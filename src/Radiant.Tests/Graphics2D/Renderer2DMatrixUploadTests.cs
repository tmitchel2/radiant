using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics;
using Radiant.Graphics2D;

namespace Radiant.Tests.Graphics2D
{
    [TestClass]
    public class Renderer2DMatrixUploadTests
    {
        /// <summary>
        /// Simulates the WGSL shader's column-vector multiply: M * v.
        /// The float[16] buffer is interpreted as column-major (4 columns of vec4),
        /// matching WGSL's mat4x4 memory layout.
        /// </summary>
        private static Vector4 GpuTransform(float[] gpuMatrix, Vector4 v)
        {
            // gpuMatrix layout: [col0.x, col0.y, col0.z, col0.w, col1.x, ...]
            // WGSL mat4x4 * vec4 = v.x * col0 + v.y * col1 + v.z * col2 + v.w * col3
            return new Vector4(
                v.X * gpuMatrix[0] + v.Y * gpuMatrix[4] + v.Z * gpuMatrix[8] + v.W * gpuMatrix[12],
                v.X * gpuMatrix[1] + v.Y * gpuMatrix[5] + v.Z * gpuMatrix[9] + v.W * gpuMatrix[13],
                v.X * gpuMatrix[2] + v.Y * gpuMatrix[6] + v.Z * gpuMatrix[10] + v.W * gpuMatrix[14],
                v.X * gpuMatrix[3] + v.Y * gpuMatrix[7] + v.Z * gpuMatrix[11] + v.W * gpuMatrix[15]);
        }

        [TestMethod]
        public void SerializeMatrixForGpu_ScreenSpaceOriginMapsToNdcTopLeft()
        {
            var camera = new Camera2D(800, 600, Handedness.LeftHanded);
            var gpuMatrix = new float[16];
            Renderer2D.SerializeMatrixForGpu(camera.GetProjectionMatrix(), gpuMatrix);

            var result = GpuTransform(gpuMatrix, new Vector4(0, 0, 0, 1));
            var ndcX = result.X / result.W;
            var ndcY = result.Y / result.W;

            Assert.AreEqual(-1f, ndcX, 0.0001f, "Origin X should map to NDC -1 (left edge)");
            Assert.AreEqual(1f, ndcY, 0.0001f, "Origin Y should map to NDC +1 (top edge)");
        }

        [TestMethod]
        public void SerializeMatrixForGpu_BottomRightMapsToNdcBottomRight()
        {
            var camera = new Camera2D(800, 600, Handedness.LeftHanded);
            var gpuMatrix = new float[16];
            Renderer2D.SerializeMatrixForGpu(camera.GetProjectionMatrix(), gpuMatrix);

            var result = GpuTransform(gpuMatrix, new Vector4(800, 600, 0, 1));
            var ndcX = result.X / result.W;
            var ndcY = result.Y / result.W;

            Assert.AreEqual(1f, ndcX, 0.0001f, "Right edge X should map to NDC +1");
            Assert.AreEqual(-1f, ndcY, 0.0001f, "Bottom edge Y should map to NDC -1");
        }

        [TestMethod]
        public void SerializeMatrixForGpu_CenterMapsToNdcOrigin()
        {
            var camera = new Camera2D(800, 600, Handedness.LeftHanded);
            var gpuMatrix = new float[16];
            Renderer2D.SerializeMatrixForGpu(camera.GetProjectionMatrix(), gpuMatrix);

            var result = GpuTransform(gpuMatrix, new Vector4(400, 300, 0, 1));
            var ndcX = result.X / result.W;
            var ndcY = result.Y / result.W;

            Assert.AreEqual(0f, ndcX, 0.0001f);
            Assert.AreEqual(0f, ndcY, 0.0001f);
        }

        [TestMethod]
        public void SerializeMatrixForGpu_WComponentRemainsOne()
        {
            // The key property: for an orthographic projection, the GPU multiply must
            // produce W=1 for all points. If the matrix is transposed incorrectly,
            // the translation leaks into the W row, causing projective distortion.
            var camera = new Camera2D(800, 600, Handedness.LeftHanded);
            var gpuMatrix = new float[16];
            Renderer2D.SerializeMatrixForGpu(camera.GetProjectionMatrix(), gpuMatrix);

            var points = new[]
            {
                new Vector4(0, 0, 0, 1),
                new Vector4(800, 600, 0, 1),
                new Vector4(400, 300, 0, 1),
                new Vector4(100, 500, 0, 1),
            };

            foreach (var point in points)
            {
                var result = GpuTransform(gpuMatrix, point);
                Assert.AreEqual(1f, result.W, 0.0001f,
                    $"W must be 1.0 for orthographic projection at ({point.X},{point.Y}), got {result.W}");
            }
        }

        [TestMethod]
        public void SerializeMatrixForGpu_MatchesCpuTransform()
        {
            // The GPU result (after perspective divide) must match the CPU-side
            // Vector4.Transform for all points.
            var camera = new Camera2D(800, 600, Handedness.LeftHanded);
            var matrix = camera.GetProjectionMatrix();
            var gpuMatrix = new float[16];
            Renderer2D.SerializeMatrixForGpu(matrix, gpuMatrix);

            var points = new[]
            {
                new Vector4(0, 0, 0, 1),
                new Vector4(800, 600, 0, 1),
                new Vector4(400, 300, 0, 1),
                new Vector4(200, 100, 0.5f, 1),
            };

            foreach (var point in points)
            {
                var cpuResult = Vector4.Transform(point, matrix);
                var gpuResult = GpuTransform(gpuMatrix, point);

                Assert.AreEqual(cpuResult.X / cpuResult.W, gpuResult.X / gpuResult.W, 0.0001f,
                    $"NDC X mismatch at ({point.X},{point.Y},{point.Z})");
                Assert.AreEqual(cpuResult.Y / cpuResult.W, gpuResult.Y / gpuResult.W, 0.0001f,
                    $"NDC Y mismatch at ({point.X},{point.Y},{point.Z})");
                Assert.AreEqual(cpuResult.Z / cpuResult.W, gpuResult.Z / gpuResult.W, 0.0001f,
                    $"NDC Z mismatch at ({point.X},{point.Y},{point.Z})");
            }
        }

        [TestMethod]
        public void SerializeMatrixForGpu_RightHandedMatchesCpuTransform()
        {
            var camera = new Camera2D(800, 600, Handedness.RightHanded);
            var matrix = camera.GetProjectionMatrix();
            var gpuMatrix = new float[16];
            Renderer2D.SerializeMatrixForGpu(matrix, gpuMatrix);

            var point = new Vector4(200, 150, 0, 1);
            var cpuResult = Vector4.Transform(point, matrix);
            var gpuResult = GpuTransform(gpuMatrix, point);

            Assert.AreEqual(cpuResult.X / cpuResult.W, gpuResult.X / gpuResult.W, 0.0001f);
            Assert.AreEqual(cpuResult.Y / cpuResult.W, gpuResult.Y / gpuResult.W, 0.0001f);
        }
    }
}
