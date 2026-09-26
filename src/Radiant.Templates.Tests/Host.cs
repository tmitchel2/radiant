using System;
using Radiant.UI.Core;

namespace Radiant.Templates.Tests;

/// <summary>A component whose build is a lambda, for tests that hold state in signals.</summary>
internal sealed record Host(Func<BuildContext, Element?> Body) : Component
{
    public override Element? Build(BuildContext context) => Body(context);
}
