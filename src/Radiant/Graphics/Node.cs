using System.Numerics;

namespace Radiant.Graphics
{
    public record Node(
        string Id,
        Vector3 Translation = default,
        Quaternion Rotation = default,
        Vector3 Scale = default);
}
