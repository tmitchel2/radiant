// Ported from msdfgen (https://github.com/Chlumsky/msdfgen) at commit 1c106ed8: the interface the artifact
// classifiers of core/MSDFErrorCorrection.cpp share as C++ template parameters.
// Copyright (c) 2014 - 2025 Viktor Chlumsky, licensed under the MIT License (see THIRD-PARTY-NOTICES.md).

namespace Radiant.Text.Msdf;

/// <summary>
/// Decides whether the median interpolated between two texels, at a point where two channels
/// cross, is an artifact: a value the true distance could not have there.
/// </summary>
internal interface IArtifactClassifier
{
    /// <summary>A candidate: the interpolated median is outside what its ends allow.</summary>
    const int FlagCandidate = 0x01;

    /// <summary>An artifact: it is even outside what the distance between the texels allows.</summary>
    const int FlagArtifact = 0x02;

    /// <summary>
    /// Tests the median <paramref name="xm"/> interpolated at <paramref name="xt"/>, between
    /// <paramref name="am"/> at <paramref name="at"/> and <paramref name="bm"/> at <paramref name="bt"/>.
    /// </summary>
    int RangeTest(double at, double bt, double xt, float am, float bm, float xm);

    /// <summary>Whether the combined results of the tests at <paramref name="t"/> indicate an artifact.</summary>
    bool Evaluate(double t, float m, int flags);
}
