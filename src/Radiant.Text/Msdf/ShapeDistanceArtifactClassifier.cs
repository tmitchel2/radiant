// Ported from msdfgen (https://github.com/Chlumsky/msdfgen) at commit 1c106ed8, core/MSDFErrorCorrection.cpp
// (ShapeDistanceChecker::ArtifactClassifier).
// Copyright (c) 2014 - 2025 Viktor Chlumsky, licensed under the MIT License (see THIRD-PARTY-NOTICES.md).

namespace Radiant.Text.Msdf;

/// <summary>
/// Classifies an artifact candidate by the exact shape distance: it is an artifact if correcting
/// the texel would bring the interpolated distance meaningfully nearer the true one.
/// </summary>
internal readonly struct ShapeDistanceArtifactClassifier(ShapeDistanceChecker parent, Vector2d direction, double span) : IArtifactClassifier
{
    public int RangeTest(double at, double bt, double xt, float am, float bm, float xm) =>
        BaseArtifactClassifier.RangeTest(span, parent.ProtectedFlag, at, bt, xt, am, bm, xm);

    public bool Evaluate(double t, float m, int flags)
    {
        if ((flags & IArtifactClassifier.FlagCandidate) != 0)
        {
            // Skip expensive distance evaluation if the point has already been classified as an artifact by the base classifier.
            if ((flags & IArtifactClassifier.FlagArtifact) != 0)
            {
                return true;
            }
            return parent.ImprovesByCorrecting(direction, t);
        }
        return false;
    }
}
