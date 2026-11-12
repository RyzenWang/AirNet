# Contributing

Thanks for helping improve AirNet. The project is a .NET 10 + Avalonia desktop app with
a small, UI-free Core library.

## Requirements

- .NET 10 SDK
- Windows for the primary development loop (the current maintainer tests Windows and
  Linux x64; macOS and Linux arm64 are not verified yet)
- PowerShell 5.1 or 7 for packaging scripts
- Optional: Python 3 with Pillow only when regenerating `packaging/assets/AirNet.icns`

## Build and test

```powershell
dotnet build AirNet.slnx -c Release -warnaserror
dotnet test tests/AirNet.Core.Tests/AirNet.Core.Tests.csproj -c Release
```

On resource-constrained Windows machines, the reliable MSBuild invocation is:

```powershell
dotnet build AirNet.slnx -c Release -warnaserror -m:1 -nodeReuse:false -p:UseSharedCompilation=false
```

## Run

```powershell
dotnet run --project src/AirNet.App
dotnet run --project src/AirNet.App -- --selftest out.txt
dotnet run --project src/AirNet.App -- --sites sites.txt
dotnet run --project src/AirNet.App -- --report report.html
```

The app starts with deterministic sample data. The sample harness uses documentation
address ranges (`192.0.2.0/24`, `198.51.100.0/24`, `203.0.113.0/24`); never commit a
real public IP, SSID, DNS server, or household network detail to the repository.

## Packaging

```powershell
powershell -File packaging/pack-all.ps1
```

This creates portable assets under `artifacts/packages`:

- Windows x64 zip
- macOS arm64/x64 `.app` zip
- Linux x64/arm64 zip and tar.gz

No setup/installer is built. Release assets are attached by the tag CI workflow; do not
upload local artifacts by hand unless the workflow failed or you are repairing a release.

## Releasing

1. Bump `<Version>` in `src/AirNet.App/AirNet.App.csproj`, plus `VersionText`
   (`SettingsViewModel`) and the footer string in `ShellView.axaml` — all three are
   separate literals today.
2. Move the `[Unreleased]` CHANGELOG entries into `## [x.y.z] - yyyy-mm-dd`.
3. Re-render screenshots (`AirNet.exe --export docs/shots --scale 1`) — the status bar
   carries the version, so they go stale the moment it changes.
4. Tag `vX.Y.Z` and push the tag; CI builds all four platform archives and attaches
   them to the release.

The release body comes from `docs/release/<tag>.md` when that file exists, otherwise it
is extracted from the matching CHANGELOG section. Write the override when the audience
needs a different tone from the changelog.

## Code style

- Keep `AirNet.Core` free of Avalonia and UI dependencies.
- Put formatting/display decisions in ViewModels, not XAML converters.
- Use `async` end-to-end; do not block the UI thread.
- Use `OperatingSystem.IsWindows()` guards around Windows-only P/Invoke/commands.
- Any new dependency needs a license entry in `THIRD_PARTY_NOTICES.md`.

## Tests

- Add a focused test for every statistic, parser, classifier, or persistence change.
- Keep tests deterministic; never depend on the host's network or a live website.
- UI behavior should be verified with a screenshot or a manual note in the PR.

## Pull requests

- Keep changes scoped and explain the user-visible effect.
- Include the commands you ran and their result.
- Do not include generated `bin/`, `obj/`, `artifacts/`, or local history files.
- For release-visible changes, update `CHANGELOG.md` and the relevant docs.

By contributing, you agree that your contribution is licensed under the repository's
MIT license.
