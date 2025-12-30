using System.Numerics;

namespace Radiant.Graphics
{
    public sealed record MeshNode(
        string Id,
        Geometry Geometry,
        Vector3 Translation = default,
        Quaternion Rotation = default,
        Vector3 Scale = default)
        : Node(Id, Translation, Rotation, Scale);
}
