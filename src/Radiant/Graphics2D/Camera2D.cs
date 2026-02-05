using System;
using System.Numerics;
using Radiant.Graphics;

namespace Radiant.Graphics2D
{
    public class Camera2D
    {
        public float Left { get; set; }
        public float Right { get; set; }
        public float Bottom { get; set; }
        public float Top { get; set; }
        public Handedness Handedness { get; }

        private readonly float _originalWidth;
        private readonly float _originalHeight;
        private readonly float _originalAspect;

        public Camera2D(float width, float height, Handedness handedness)
        {
            if (handedness != Handedness.LeftHanded && handedness != Handedness.RightHanded)
                throw new ArgumentOutOfRangeException(nameof(handedness));

            Handedness = handedness;
            // Store original dimensions and aspect ratio
            _originalWidth = width;
            _originalHeight = height;
            _originalAspect = width / height;

            // Screen-space: origin at top-left, Y increases downward
            Left = 0;
            Right = width;
            Bottom = height;
            Top = 0;
        }

        public Matrix4x4 GetProjectionMatrix()
        {
            return Handedness switch
            {
                Handedness.LeftHanded => Matrix4x4.CreateOrthographicOffCenterLeftHanded(
                    Left, Right, Bottom, Top, -1.0f, 1.0f),
                Handedness.RightHanded => Matrix4x4.CreateOrthographicOffCenter(
                    Left, Right, Bottom, Top, -1.0f, 1.0f),
                _ => throw new InvalidOperationException($"Unexpected handedness: {Handedness}")
            };
        }

        public void SetViewportSize(float width, float height)
        {
            // Maintain original aspect ratio with letterboxing/pillarboxing
            var newAspect = width / height;

            if (newAspect > _originalAspect)
            {
                // Window is wider than original - keep height, expand width
                Left = 0;
                Right = _originalHeight * newAspect;
                Top = 0;
                Bottom = _originalHeight;
            }
            else
            {
                // Window is taller than original - keep width, expand height
                Left = 0;
                Right = _originalWidth;
                Top = 0;
                Bottom = _originalWidth / newAspect;
            }
        }
    }
}
