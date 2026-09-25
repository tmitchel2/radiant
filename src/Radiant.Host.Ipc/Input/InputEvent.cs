namespace Radiant.Host.Ipc.Input;

/// <summary>
/// One discrete input event dequeued from an <see cref="InputRing"/>.
/// </summary>
/// <param name="Kind">What happened.</param>
/// <param name="Code">Button id / key id / character code unit, depending on <paramref name="Kind"/>.</param>
/// <param name="X">Mouse X in renderer-content pixels (scroll: delta X).</param>
/// <param name="Y">Mouse Y in renderer-content pixels (scroll: delta Y).</param>
public readonly record struct InputEvent(InputEventKind Kind, int Code, float X, float Y);
