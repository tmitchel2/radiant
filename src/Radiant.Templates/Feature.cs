namespace Radiant.Templates;

/// <summary>One feature of a <see cref="FeatureGrid"/>.</summary>
/// <param name="Icon">Its icon.</param>
/// <param name="Title">Its name.</param>
/// <param name="Text">A sentence about it.</param>
public sealed record Feature(string Icon, string Title, string Text);
