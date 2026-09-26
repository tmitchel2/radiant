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
        // For the menu bar checks: a command on a menu of its own.
        context.UseCommand(new Command("hello", "Say hello") { Menu = "Test", Shortcut = KeyChord.Command(KeyCode.J), Run = () => events.Add("command:hello") });
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
            Children =
            [
                new Radiant.Components.CommandMenuBar(),
                // For the accessibility checks: a button VoiceOver can press, and text it can read.
                new Box
                {
                    Focusable = true,
                    Semantics = new Semantics { Role = SemanticsRole.Button, Label = "Press me" },
                    Layout = new LayoutStyle { Width = 120, Height = 32, Margin = Edges.All(20) },
                    OnClick = _ => events.Add("click:Press me"),
                },
                new TextBlock("Hello from Radiant") { Layout = new LayoutStyle { Margin = new Edges(20, 0, 0, 0) } },
            ],
        };
    }
}
