using System;
using System.Collections.Generic;
using Radiant.Layout;
using Radiant.UI.Core;

namespace Radiant.PlatformCheck;

/// <summary>
/// The self-test's window: a focusable box that records the key and text events reaching the
/// UI, and an effect that runs <see cref="SelfTest"/> on the first frame and exits with its
/// result.
/// </summary>
internal sealed record SelfTestApp : Component
{
    public override Element? Build(BuildContext context)
    {
        var events = context.UseRef(new List<string>()).Value;
        var target = context.UseRef(new ElementRef()).Value;
        var platform = context.UsePlatform();
        var root = context.Root;
        context.UseEffect(() =>
        {
            var failures = SelfTest.Run(platform, root, target, events);
            Console.WriteLine(failures == 0 ? "All checks passed." : $"{failures} checks failed.");
            // The run loop can't be ended from inside the UI yet; the result is all that matters.
            Environment.Exit(failures == 0 ? 0 : 1);
            return null;
        }, default(ValueTuple));
        return new Box
        {
            Ref = target,
            Focusable = true,
            Layout = new LayoutStyle { FlexGrow = 1 },
            OnKeyDown = e => events.Add($"key:{e.Key}"),
            OnTextInput = e => events.Add($"text:{e.Text}"),
        };
    }
}
