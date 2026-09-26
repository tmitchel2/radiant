using System;
using System.IO;
using System.Linq;
using Radiant.Platform.MacOS;
using Radiant.PlatformCheck;
using Radiant.Text;
using Radiant.Theming;
using Radiant.UI.Core;

// platform-check              a window for checking input methods, cursors, appearance, clipboard and dialogs by hand
// platform-check --selftest   checks text input against GLFW's real view, prints PASS/FAIL, exits non-zero on failure
if (!MacPlatform.IsSupported)
{
    Console.Error.WriteLine("The platform check needs macOS.");
    return 1;
}

if (args.Contains("--selftest"))
{
    RadiantUI.Run(new SelfTestApp(), new UIAppOptions { Title = "Radiant platform self-test", Width = 480, Height = 320, Platform = MacPlatform.CreateOrHeadless });
    return 1; // SelfTestApp exits with the result; reaching here means it never ran
}

// Radiant embeds only Latin fonts and has no system font fallback yet; borrow a macOS font that
// covers kana, CJK and Hangul, so what the input method composes is readable.
const string cjkFont = "/System/Library/Fonts/Supplemental/Arial Unicode.ttf";
if (File.Exists(cjkFont))
{
    FontLibrary.Default.Register(FontFace.FromFile(cjkFont, "Arial Unicode"), fallback: true);
}

var themes = new ThemeController();
RadiantUI.Run(
    new ThemeProvider(themes, new CheckApp(new ImeFieldModel())) { FollowAppearance = true },
    new UIAppOptions { Title = "Radiant platform check", Width = 960, Height = 820, Platform = MacPlatform.CreateOrHeadless });
return 0;
