using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>A page on a <see cref="Navigator"/>'s stack.</summary>
/// <param name="Title">What the app bar says over it.</param>
/// <param name="Content">The page.</param>
public sealed record NavigatorPage(string Title, Element Content);
