using System;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics;
using Radiant.Graphics2D;

namespace Radiant.Tests.Graphics2D
{
    [TestClass]
    public class Camera2DTests
    {
        [TestMethod]
        public void Constructor_InitializesWithCorrectBounds()
        {
            var camera = new Camera2D(800, 600, Handedness.LeftHanded);

            Assert.AreEqual(0f, camera.Left);
            Assert.AreEqual(800f, camera.Right);
            Assert.AreEqual(600f, camera.Bottom);
            Assert.AreEqual(0f, camera.Top);
        }

        [TestMethod]
        public void GetProjectionMatrix_ReturnsLeftHandedMatrix()
        {
            var camera = new Camera2D(800, 600, Handedness.LeftHanded);
            var matrix = camera.GetProjectionMatrix();

            // Verify the matrix is left-handed by checking the Z transformation
            // In left-handed orthographic projection, the Z component should be positive
            // when transforming points from near to far plane

            // Test point at near plane (z = -1)
            var nearPoint = new Vector4(0, 0, -1, 1);
            var transformedNear = Vector4.Transform(nearPoint, matrix);
            var ndcNear = transformedNear.Z / transformedNear.W;

            // Test point at far plane (z = 1)
            var farPoint = new Vector4(0, 0, 1, 1);
            var transformedFar = Vector4.Transform(farPoint, matrix);
            var ndcFar = transformedFar.Z / transformedFar.W;

            // In left-handed system, far plane (z=1) should map to higher NDC value than near plane (z=-1)
            Assert.IsTrue(ndcFar > ndcNear,
                $"Left-handed system should have far plane ({ndcFar}) > near plane ({ndcNear})");
        }

        [TestMethod]
        public void GetProjectionMatrix_MapsNearPlaneToZero()
        {
            var camera = new Camera2D(800, 600, Handedness.LeftHanded);
            var matrix = camera.GetProjectionMatrix();

            // Test point at near plane (z = -1)
            var nearPoint = new Vector4(0, 0, -1, 1);
            var transformedNear = Vector4.Transform(nearPoint, matrix);
            var ndcZ = transformedNear.Z / transformedNear.W;

            // In left-handed system, near plane should map to 0 in NDC space
            Assert.AreEqual(0f, ndcZ, 0.0001f);
        }

        [TestMethod]
        public void GetProjectionMatrix_MapsFarPlaneToOne()
        {
            var camera = new Camera2D(800, 600, Handedness.LeftHanded);
            var matrix = camera.GetProjectionMatrix();

            // Test point at far plane (z = 1)
            var farPoint = new Vector4(0, 0, 1, 1);
            var transformedFar = Vector4.Transform(farPoint, matrix);
            var ndcZ = transformedFar.Z / transformedFar.W;

            // Far plane should map to 1 in NDC space
            Assert.AreEqual(1f, ndcZ, 0.0001f);
        }

        [TestMethod]
        public void GetProjectionMatrix_TransformsScreenCoordinatesCorrectly()
        {
            var camera = new Camera2D(800, 600, Handedness.LeftHanded);
            var matrix = camera.GetProjectionMatrix();

            // Test corners of the screen (screen-space: origin top-left, Y-down)
            // Top-left corner (0, 0)
            var topLeft = new Vector4(0, 0, 0, 1);
            var transformedTL = Vector4.Transform(topLeft, matrix);
            Assert.AreEqual(-1f, transformedTL.X / transformedTL.W, 0.0001f);
            Assert.AreEqual(1f, transformedTL.Y / transformedTL.W, 0.0001f);

            // Bottom-right corner (800, 600)
            var bottomRight = new Vector4(800, 600, 0, 1);
            var transformedBR = Vector4.Transform(bottomRight, matrix);
            Assert.AreEqual(1f, transformedBR.X / transformedBR.W, 0.0001f);
            Assert.AreEqual(-1f, transformedBR.Y / transformedBR.W, 0.0001f);
        }

        [TestMethod]
        public void GetProjectionMatrix_CenterPointMapsToMiddleDepth()
        {
            var camera = new Camera2D(800, 600, Handedness.LeftHanded);
            var matrix = camera.GetProjectionMatrix();

            // Center of screen (400, 300) should map to center in NDC
            var center = new Vector4(400, 300, 0, 1);
            var transformed = Vector4.Transform(center, matrix);

            Assert.AreEqual(0f, transformed.X / transformed.W, 0.0001f);
            Assert.AreEqual(0f, transformed.Y / transformed.W, 0.0001f);
            // In left-handed system, z=0 (middle of near/far) maps to NDC z=0.5
            Assert.AreEqual(0.5f, transformed.Z / transformed.W, 0.0001f);
        }

        [TestMethod]
        public void SetViewportSize_MaintainsAspectRatio_WiderWindow()
        {
            var camera = new Camera2D(800, 600, Handedness.LeftHanded);
            camera.SetViewportSize(1600, 600); // Twice as wide

            // Height should remain the same, width should expand
            Assert.AreEqual(0f, camera.Left);
            Assert.AreEqual(0f, camera.Top);
            Assert.AreEqual(600f, camera.Bottom);
            Assert.IsTrue(camera.Right > 800f);
        }

        [TestMethod]
        public void SetViewportSize_MaintainsAspectRatio_TallerWindow()
        {
            var camera = new Camera2D(800, 600, Handedness.LeftHanded);
            camera.SetViewportSize(800, 1200); // Twice as tall

            // Width should remain the same, height should expand
            Assert.AreEqual(0f, camera.Left);
            Assert.AreEqual(800f, camera.Right);
            Assert.AreEqual(0f, camera.Top);
            Assert.IsTrue(camera.Bottom > 600f);
        }

        [TestMethod]
        public void LeftHandedCoordinateSystem_PositiveZPointsIntoScreen()
        {
            // This test verifies the fundamental property of left-handed coordinate systems:
            // With X pointing right and Y pointing up, Z points into the screen (away from viewer)

            var camera = new Camera2D(100, 100, Handedness.LeftHanded);
            var matrix = camera.GetProjectionMatrix();

            // Two points at same X,Y but different Z
            var point1 = new Vector4(0, 0, -0.5f, 1); // Closer to viewer
            var point2 = new Vector4(0, 0, 0.5f, 1);  // Further from viewer (into screen)

            var transformed1 = Vector4.Transform(point1, matrix);
            var transformed2 = Vector4.Transform(point2, matrix);

            var ndcZ1 = transformed1.Z / transformed1.W;
            var ndcZ2 = transformed2.Z / transformed2.W;

            // In left-handed system, point further into screen (positive Z)
            // should have larger NDC Z value
            Assert.IsTrue(ndcZ2 > ndcZ1,
                "In left-handed system, positive Z should point into screen (away from viewer)");
        }

        [TestMethod]
        public void LeftHandedCoordinateSystem_CrossProductFollowsLeftHandRule()
        {
            // Verify that the coordinate system follows left-hand rule
            // In left-handed system: X × Y = Z (pointing into screen)

            var xAxis = new Vector3(1, 0, 0);
            var yAxis = new Vector3(0, 1, 0);
            var cross = Vector3.Cross(xAxis, yAxis);

            // In left-handed system, X cross Y should give positive Z (into screen)
            Assert.AreEqual(0f, cross.X, 0.0001f);
            Assert.AreEqual(0f, cross.Y, 0.0001f);
            Assert.AreEqual(1f, cross.Z, 0.0001f);
        }

        [TestMethod]
        public void Constructor_StoresHandedness_LeftHanded()
        {
            var camera = new Camera2D(800, 600, Handedness.LeftHanded);
            Assert.AreEqual(Handedness.LeftHanded, camera.Handedness);
        }

        [TestMethod]
        public void Constructor_StoresHandedness_RightHanded()
        {
            var camera = new Camera2D(800, 600, Handedness.RightHanded);
            Assert.AreEqual(Handedness.RightHanded, camera.Handedness);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Constructor_RejectsDefaultHandedness()
        {
            _ = new Camera2D(800, 600, default);
        }

        [TestMethod]
        public void RightHanded_GetProjectionMatrix_ReturnsRightHandedMatrix()
        {
            var camera = new Camera2D(800, 600, Handedness.RightHanded);
            var matrix = camera.GetProjectionMatrix();

            // Test point at near plane (z = -1) and far plane (z = 1)
            var nearPoint = new Vector4(0, 0, -1, 1);
            var transformedNear = Vector4.Transform(nearPoint, matrix);
            var ndcNear = transformedNear.Z / transformedNear.W;

            var farPoint = new Vector4(0, 0, 1, 1);
            var transformedFar = Vector4.Transform(farPoint, matrix);
            var ndcFar = transformedFar.Z / transformedFar.W;

            // In right-handed system, near plane should map to higher NDC Z than far plane
            // (opposite of left-handed)
            Assert.IsTrue(ndcNear > ndcFar,
                $"Right-handed system should have near plane ({ndcNear}) > far plane ({ndcFar})");
        }

        [TestMethod]
        public void RightHanded_PositiveZPointsTowardViewer()
        {
            var camera = new Camera2D(100, 100, Handedness.RightHanded);
            var matrix = camera.GetProjectionMatrix();

            // Two points at same X,Y but different Z
            var point1 = new Vector4(0, 0, -0.5f, 1); // Further from viewer
            var point2 = new Vector4(0, 0, 0.5f, 1);  // Closer to viewer (toward viewer)

            var transformed1 = Vector4.Transform(point1, matrix);
            var transformed2 = Vector4.Transform(point2, matrix);

            var ndcZ1 = transformed1.Z / transformed1.W;
            var ndcZ2 = transformed2.Z / transformed2.W;

            // In right-handed system, positive Z points toward viewer
            // so point2 (positive Z) should have smaller NDC Z (closer to near plane)
            Assert.IsTrue(ndcZ1 > ndcZ2,
                "In right-handed system, positive Z should point toward viewer");
        }

        [TestMethod]
        public void RightHanded_XYMappingIsIdenticalToLeftHanded()
        {
            var leftCamera = new Camera2D(800, 600, Handedness.LeftHanded);
            var rightCamera = new Camera2D(800, 600, Handedness.RightHanded);

            var leftMatrix = leftCamera.GetProjectionMatrix();
            var rightMatrix = rightCamera.GetProjectionMatrix();

            // Test that X and Y mapping are identical
            var testPoint = new Vector4(200, 150, 0, 1);

            var leftTransformed = Vector4.Transform(testPoint, leftMatrix);
            var rightTransformed = Vector4.Transform(testPoint, rightMatrix);

            Assert.AreEqual(leftTransformed.X / leftTransformed.W,
                rightTransformed.X / rightTransformed.W, 0.0001f,
                "X mapping should be identical");
            Assert.AreEqual(leftTransformed.Y / leftTransformed.W,
                rightTransformed.Y / rightTransformed.W, 0.0001f,
                "Y mapping should be identical");
        }
    }
}
