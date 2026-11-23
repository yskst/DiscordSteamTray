# Agent Instructions

## Project

- This is a Windows notification-area application built with C# and .NET 8 (`net8.0-windows`).
- It uses WinForms `ApplicationContext` and `NotifyIcon`; do not add a visible main window.
- Keep changes focused and preserve the existing tray-based UX.

## Architecture

- `Program.cs` loads the saved settings and starts `TrayApplicationContext`.
- `TrayApplicationContext.cs` owns the tray icon, Discord.Net client, Steam polling loop, and service shutdown.
- `SettingsForm.cs` and `SettingsHelpForm.cs` provide the settings UI and setup instructions.
- `SettingsStore.cs` protects settings with Windows DPAPI for the current user. `TraySettings.cs` validates values.
- `.github/workflows/windows-build.yml` builds the self-contained Windows x64 executable and creates releases for version tags.

## Security and assets

- Use a Discord bot token, never a Discord user token.
- Never add credentials to source, logs, documentation examples, or commits. The settings file is encrypted and stored under the current user's Local Application Data directory.
- Keep Discord presence/member intents enabled as required by the app.
- Do not add Discord or Steam logos or third-party artwork. Keep the tray icon original and generated in code.
- Dispose GDI objects and release native icon handles created by `Bitmap.GetHicon()` with `DestroyIcon`.

## Code and UI

- Keep all C# user-facing strings, errors, and logs in English.
- Follow existing nullable reference type, async, and resource-disposal patterns.
- Surface actionable errors; avoid broad catches and silent fallbacks.
- Keep status color mapping and left/right service identity intact when changing the tray icon.

## Build and validation

Run a Release build:

```powershell
dotnet build .\DiscordSteamTray.csproj -c Release
```

Publish the self-contained Windows x64 executable:

```powershell
dotnet publish .\DiscordSteamTray.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:DebugType=None -o .\publish
```

Build output and publish directories are ignored by Git. Do not remove or overwrite a running executable; use a separate output directory when it is locked.

## Changelog and releases

- Keep `README.md` and `README.en.md` in sync for user-facing setup and workflow changes.
- Add release notes under a version heading in `CHANGELOG.md`. A tag such as `v1.2.3` requires a matching `## [1.2.3]` section.
- Pushing a `vMAJOR.MINOR.PATCH` tag triggers the Windows build and creates a GitHub Release with the changelog section and executable.
