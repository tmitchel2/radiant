# Vendored: Yoga.Net

This directory is a **vendored copy** of a third-party library, not first-party Radiant code.

| | |
|---|---|
| **Upstream** | https://github.com/chenrensong/Yoga.Net |
| **Pinned commit** | `baf14fcd6cbf21d8930a297e32ef3b76674c37bd` |
| **Upstream version** | 3.2.3 (tracks Meta Yoga 3.2.x) |
| **License** | MIT (see `LICENSE`) — © Chen Ren Song; © Meta Platforms, Inc. and affiliates |
| **What it is** | A faithful pure-C# port of Meta's Yoga layout engine: Flexbox + CSS Grid, zero external dependencies, AOT/trim-safe, zero reflection / zero LINQ. |

## Why vendored (not NuGet)

Dynamis ships `PublishAot=true` and references `Radiant.csproj` directly via `ProjectReference`.
Vendoring the source (rather than a NuGet package) lets us pin an exact commit, keep the upstream
test suite running alongside ours as a correctness backstop, and guarantee the build is the
AOT/trim-clean source we reviewed — with no external version axis we don't control.

## Local modifications

Kept deliberately minimal so re-vendoring is cheap:

- `Yoga.Net.csproj` — trimmed from the upstream **packaging** csproj: single `TargetFramework`
  (`net10.0`, matching Radiant) instead of `net8;net9;net10`, `IsPackable=false`, NuGet package
  metadata removed. AOT/trim flags (`IsAotCompatible`, `IsTrimmable`, `EnableTrimAnalyzer`) preserved.
- `Directory.Build.props` (added) — shields this third-party code from Radiant's strict
  `src/Directory.Build.props` (Roslynator + warnings-as-errors). Engine `.cs` source is **unmodified**.
- `Yoga.Net.Tests/Yoga.Net.Tests.csproj` — `ProjectReference` path fixed for the sibling layout
  (`..\Yoga.Net\` instead of `..\..\src\Yoga.Net\`).

## How to re-vendor (bump the pin)

```bash
git clone https://github.com/chenrensong/Yoga.Net /tmp/Yoga.Net
# inspect the new commit, then:
cp -R /tmp/Yoga.Net/src/Yoga.Net/*.cs   lib/radiant/src/Yoga.Net/          # engine source only
cp -R /tmp/Yoga.Net/src/Yoga.Net/*/     lib/radiant/src/Yoga.Net/          # subfolders
cp -R /tmp/Yoga.Net/tests/Yoga.Net.Tests/*.cs lib/radiant/src/Yoga.Net.Tests/
# re-apply the csproj/Directory.Build.props edits above; update the pinned commit in this file.
```

Do **not** edit the engine `.cs` files locally — keep this a clean vendored copy so upstream
fixes apply without merge pain. Radiant-side adaptation lives in `Radiant/Layout/` (the wrapper),
never in here.
