namespace Radiant.UI.Core;

/// <summary>A hook's storage, kept by position in the component across builds; released when it unmounts.</summary>
internal interface IHook
{
    void Release();
}
