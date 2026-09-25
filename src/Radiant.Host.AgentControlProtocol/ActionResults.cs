namespace Radiant.Host.AgentControlProtocol;

/// <summary>
/// Result records for the action handlers every application has. AOT-safe via
/// <see cref="AgentJsonContext"/>. An application's own results belong in its own
/// <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>, combined with this one through
/// <c>JsonTypeInfoResolver.Combine</c>.
/// </summary>
public sealed record BoolResult(bool Ok);

public sealed record ExitResult(string Status);

public sealed record ScreenshotResult(string Path, int Width, int Height);
