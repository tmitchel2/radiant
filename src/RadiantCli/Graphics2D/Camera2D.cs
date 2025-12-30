using System.Numerics;

namespace RadiantCli.Graphics2D
{
    public class Camera2D
    {
        public float Left { get; set; }
        public float Right { get; set; }
        public float Bottom { get; set; }
        public float Top { get; set; }

        public Camera2D(float width, float height)
        {
            // Default: origin at center, 1:1 pixel mapping at default zoom
            Left = -width / 2f;
            Right = width / 2f;
            Bottom = -height / 2f;
            Top = height / 2f;
        }

        public Matrix4x4 GetProjectionMatrix()
        {
            return Matrix4x4.CreateOrthographicOffCenter(
                Left, Right, Bottom, Top, -1.0f, 1.0f);
        }

        public void SetViewportSize(float width, float height)
        {
            var aspectRatio = width / height;
            var currentAspect = (Right - Left) / (Top - Bottom);

            if (aspectRatio > currentAspect)
            {
                // Wider viewport - expand left/right
                var centerX = (Left + Right) / 2f;
                var halfHeight = (Top - Bottom) / 2f;
                var halfWidth = halfHeight * aspectRatio;
                Left = centerX - halfWidth;
                Right = centerX + halfWidth;
            }
            else
            {
                // Taller viewport - expand top/bottom
                var centerY = (Bottom + Top) / 2f;
                var halfWidth = (Right - Left) / 2f;
                var halfHeight = halfWidth / aspectRatio;
                Bottom = centerY - halfHeight;
                Top = centerY + halfHeight;
            }
        }
    }
}
