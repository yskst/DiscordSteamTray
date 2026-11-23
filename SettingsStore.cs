using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DiscordSteamTray;

internal static class SettingsStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DiscordSteamTray.Settings.v1");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DiscordSteamTray",
        "settings.dat");

    public static TraySettings? Load()
    {
        string path = SettingsPath;
        if (!File.Exists(path))
        {
            return null;
        }

        byte[] protectedData = File.ReadAllBytes(path);
        byte[] clearData = ProtectedData.Unprotect(
            protectedData,
            Entropy,
            DataProtectionScope.CurrentUser);
        TraySettings storedSettings = JsonSerializer.Deserialize<TraySettings>(clearData, JsonOptions)
            ?? throw new InvalidDataException("Could not read the saved settings.");
        return TraySettings.Create(
            storedSettings.DiscordBotToken,
            storedSettings.DiscordGuildId.ToString(),
            storedSettings.DiscordUserId.ToString(),
            storedSettings.SteamApiKey,
            storedSettings.SteamId64.ToString(),
            storedSettings.StartWithWindows);
    }

    public static void Save(TraySettings settings)
    {
        string path = SettingsPath;
        string? directory = Path.GetDirectoryName(path);
        if (directory is null)
        {
            throw new InvalidOperationException("Could not determine the settings file location.");
        }

        Directory.CreateDirectory(directory);
        byte[] clearData = JsonSerializer.SerializeToUtf8Bytes(settings);
        byte[] protectedData = ProtectedData.Protect(
            clearData,
            Entropy,
            DataProtectionScope.CurrentUser);

        string temporaryPath = path + ".tmp";
        try
        {
            File.WriteAllBytes(temporaryPath, protectedData);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
