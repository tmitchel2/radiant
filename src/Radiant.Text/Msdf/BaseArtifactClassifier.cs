// Ported from msdfgen (https://github.com/Chlumsky/msdfgen) at commit 1c106ed8, core/MSDFErrorCorrection.cpp.
// Copyright (c) 2014 - 2025 Viktor Chlumsky, licensed under the MIT License (see THIRD-PARTY-NOTICES.md).

namespace Radiant.Text.Msdf;

/// <summary>Recognizes artifacts from the contents of the distance field alone.</summary>
/// <param name="span">The largest change of stored value expected between the two texels.</param>
/// <param name="protectedFlag">Whether the texel is protected: then only inversions (a change of side) count.</param>
internal readonly struct BaseArtifactClassifier(double span, bool protectedFlag) : IArtifactClassifier
{
    public int RangeTest(double at, double bt, double xt, float am, float bm, float xm) =>
        RangeTest(span, protectedFlag, at, bt, xt, am, bm, xm);

    public bool Evaluate(double t, float m, int flags) => (flags & 2) != 0;

    /// <summary>The shared range test: <see cref="IArtifactClassifier.RangeTest"/>.</summary>
    public static int RangeTest(double span, bool protectedFlag, double at, double bt, double xt, float am, float bm, float xm)
    {
        // For protected texels, only consider inversion artifacts (interpolated median has different sign than boundaries). For the rest, it is sufficient that the interpolated median is outside its boundaries.
        if ((am > .5f && bm > .5f && xm <= .5f) || (am < .5f && bm < .5f && xm >= .5f) || (!protectedFlag && MsdfErrorCorrection.Median(am, bm, xm) != xm))
        {
            double axSpan = (xt - at) * span, bxSpan = (bt - xt) * span;
            // Check if the interpolated median's value is in the expected range based on its distance (span) from boundaries a, b.
            if (!(xm >= am - axSpan && xm <= am + axSpan && xm >= bm - bxSpan && xm <= bm + bxSpan))
            {
                return IArtifactClassifier.FlagCandidate | IArtifactClassifier.FlagArtifact;
            }
            return IArtifactClassifier.FlagCandidate;
        }
        return 0;
    }
}
