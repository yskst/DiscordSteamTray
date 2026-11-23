# Discord Steam Tray

A .NET 8 Windows Forms application that displays Discord and Steam presence in the Windows notification area. It runs without a main window, monitors Discord through a bot account, and polls the Steam Web API.

## Prerequisites and setup

1. Install the .NET 8 SDK if you intend to build or run the project from source.
2. Create a bot in the [Discord Developer Portal](https://discord.com/developers/applications). Enable **Presence Intent** and **Server Members Intent**. Under **OAuth2 > URL Generator**, select the `bot` scope, open the generated URL, and invite the bot to the target guild. Your Discord account needs Manage Server permission. The bot must share a guild with the user you want to monitor.
3. Obtain a Steam Web API key from the [Steam Web API Key page](https://steamcommunity.com/dev/apikey) and the target account's SteamID64. The Steam profile must be visible to the API.
4. Launch the application. On first run, click **How to find these values** in the settings window to see the in-app instructions. The settings window also has links to the official credential pages. For the Steam API key's Domain Name, enter `localhost` because this is a local desktop app. The app does not retrieve credentials or browser login data automatically.

Settings are encrypted with Windows DPAPI for the current Windows user and stored at `%LOCALAPPDATA%\DiscordSteamTray\settings.dat`. Run the app under the same Windows account used to configure it. Bot tokens and API keys should be kept private.

Discord Invisible is shown as Offline. Steam persona states 4 (Snooze) maps to Away, while states 5/6 map to Online.

## Run and publish

Run from source:

```powershell
dotnet run --project .\DiscordSteamTray.csproj
```

Build the Release configuration:

```powershell
dotnet build .\DiscordSteamTray.csproj -c Release
```

Output: `bin\Release\net8.0-windows\DiscordSteamTray.exe`

Publish a self-contained, single-file Windows x64 executable:

```powershell
dotnet publish .\DiscordSteamTray.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:DebugType=None -o .\publish
```

Run `publish\DiscordSteamTray.exe` directly. The published executable is self-contained and does not require the .NET SDK. Use the tray icon's context menu to refresh statuses, open settings, or exit. Saving changed settings restarts the app to apply them.

## Troubleshooting

If `dotnet run` reports `No .NET SDKs were found`, install the **.NET 8 SDK for Windows x64** (the runtime alone is not sufficient). Open a new terminal and verify that `dotnet --list-sdks` lists an `8.0.xxx` SDK before running the command again. If you are using the published application, launch `publish\DiscordSteamTray.exe` directly instead of using `dotnet run`.
