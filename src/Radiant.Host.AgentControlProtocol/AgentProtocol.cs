namespace Radiant.Host.AgentControlProtocol;

/// <summary>The agent protocol's version and its error codes.</summary>
public static class AgentProtocol
{
    /// <summary>
    /// The agent protocol version: 1 is the file transport with the host's <c>tab.*</c> actions; 2 adds
    /// the socket transport, the dispatcher and the <c>ui.*</c>/<c>app.*</c>/<c>log.*</c> actions. Adding
    /// actions or fields doesn't change it; changing or removing them does.
    /// </summary>
    public const int Version = 2;

    /// <summary>How long a command may take when it doesn't say, in milliseconds.</summary>
    public const int DefaultTimeoutMs = 10_000;
}

/// <summary>The <see cref="AgentError.Code"/> values.</summary>
public static class AgentErrorCodes
{
    /// <summary>The params are missing, malformed or out of range.</summary>
    public const string InvalidParams = "invalid_params";

    /// <summary>No action has that name.</summary>
    public const string NotFound = "not_found";

    /// <summary>No element matches the selector.</summary>
    public const string NoMatch = "no_match";

    /// <summary>More than one element matches a selector that must name one.</summary>
    public const string Ambiguous = "ambiguous";

    /// <summary>The element can't be brought into view.</summary>
    public const string NotVisible = "not_visible";

    /// <summary>The element is in view but something else is on top of it.</summary>
    public const string NotHittable = "not_hittable";

    /// <summary>The command didn't finish in time.</summary>
    public const string Timeout = "timeout";

    /// <summary>The app didn't become idle.</summary>
    public const string Busy = "busy";

    /// <summary>The instance can't do it here: no GPU for a screenshot, say.</summary>
    public const string Unsupported = "unsupported";

    /// <summary>The command was cancelled, or its client went away.</summary>
    public const string Cancelled = "cancelled";

    /// <summary>The response couldn't be read.</summary>
    public const string ParseError = "parse_error";

    /// <summary>The instance couldn't be reached.</summary>
    public const string Unreachable = "unreachable";

    /// <summary>Anything else: the action threw.</summary>
    public const string Internal = "internal";
}
