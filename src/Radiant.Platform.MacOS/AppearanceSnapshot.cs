namespace Radiant.Platform.MacOS;

/// <summary>The appearance settings read at one moment, compared to spot a change.</summary>
internal readonly record struct AppearanceSnapshot(bool IsDark, uint AccentColor, bool IncreaseContrast, bool ReduceMotion);
