using System.Reflection;
using Microsoft.Win32;

namespace DiscordSteamTray;

internal static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DiscordSteamTray";

    public static void SetEnabled(bool enabled)
    {
        using RegistryKey? runKey = enabled
            ? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            : Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);

        if (runKey is null)
        {
            if (!enabled)
            {
                return;
            }

            throw new InvalidOperationException("Could not open the Windows startup registry key.");
        }

        if (enabled)
        {
            runKey.SetValue(ValueName, GetStartupCommand(), RegistryValueKind.String);
        }
        else
        {
            runKey.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    private static string GetStartupCommand()
    {
        string executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not determine the application executable path.");

        if (Path.GetFileNameWithoutExtension(executablePath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            string? assemblyPath = Assembly.GetEntryAssembly()?.Location;
            if (string.IsNullOrWhiteSpace(assemblyPath))
            {
                throw new InvalidOperationException("Could not determine the application assembly path.");
            }

            return $"\"{executablePath}\" \"{assemblyPath}\"";
        }

        return $"\"{executablePath}\"";
    }
}
