// Copyright 2023 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

namespace Radiant.MaterialColor;

/// <summary>
/// A constraint between two dynamic colors: their tones must be a certain distance apart.
/// <para>
/// Prefer a <see cref="DynamicColor"/> with a background. This is for the special cases where
/// designers want tonal distance, literally contrast, between two colors that don't have a
/// background / foreground relationship or a contrast guarantee.
/// </para>
/// </summary>
/// <param name="RoleA">The first role in the pair.</param>
/// <param name="RoleB">The second role in the pair.</param>
/// <param name="Delta">Required difference between tones: an absolute value; negative values have undefined behavior.</param>
/// <param name="Polarity">
/// How A relates to B. For instance <c>(A, B, 15, Darker, Exact)</c> states that A's tone should be
/// exactly 15 darker than B's.
/// </param>
/// <param name="StayTogether">
/// Whether the two roles should stay on the same side of the "awkward zone" (T50–59). Necessary
/// where one role has two backgrounds.
/// </param>
/// <param name="Constraint">How the delta is enforced.</param>
public sealed record ToneDeltaPair(
    DynamicColor RoleA,
    DynamicColor RoleB,
    double Delta,
    TonePolarity Polarity,
    bool StayTogether,
    DeltaConstraint Constraint = DeltaConstraint.Exact);
