using System.Numerics;

namespace Radiant.Graphics2D
{
    public class Camera2D
    {
        public float Left { get; set; }
        public float Right { get; set; }
        public float Bottom { get; set; }
        public float Top { get; set; }

        private readonly float _originalWidth;
        private readonly float _originalHeight;
        private readonly float _originalAspect;

        public Camera2D(float width, float height)
        {
            // Store original dimensions and aspect ratio
            _originalWidth = width;
            _originalHeight = height;
            _originalAspect = width / height;

            // Default: origin at center, 1:1 pixel mapping at default zoom
            Left = -width / 2f;
            Right = width / 2f;
            Bottom = -height / 2f;
            Top = height / 2f;
        }

        public Matrix4x4 GetProjectionMatrix()
        {
            // Use left-handed coordinate system
            return Matrix4x4.CreateOrthographicOffCenterLeftHanded(
                Left, Right, Bottom, Top, -1.0f, 1.0f);
        }

        public void SetViewportSize(float width, float height)
        {
            // Maintain original aspect ratio with letterboxing/pillarboxing
            var newAspect = width / height;

            if (newAspect > _originalAspect)
            {
                // Window is wider than original - add letterboxing (pillarboxing on sides)
                // Keep original height, expand width
                var halfHeight = _originalHeight / 2f;
                var halfWidth = halfHeight * newAspect;
                Left = -halfWidth;
                Right = halfWidth;
                Bottom = -halfHeight;
                Top = halfHeight;
            }
            else
            {
                // Window is taller than original - add pillarboxing (letterboxing on top/bottom)
                // Keep original width, expand height
                var halfWidth = _originalWidth / 2f;
                var halfHeight = halfWidth / newAspect;
                Left = -halfWidth;
                Right = halfWidth;
                Bottom = -halfHeight;
                Top = halfHeight;
            }
        }
    }
}
