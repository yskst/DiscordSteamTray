namespace DiscordSteamTray;

internal sealed record TraySettings(
    string DiscordBotToken,
    ulong DiscordGuildId,
    ulong DiscordUserId,
    string SteamApiKey,
    ulong SteamId64,
    bool StartWithWindows)
{
    public static TraySettings Create(
        string discordBotToken,
        string discordGuildId,
        string discordUserId,
        string steamApiKey,
        string steamId64,
        bool startWithWindows)
    {
        return new TraySettings(
            Required(discordBotToken, "Discord Bot Token"),
            ParseId(discordGuildId, "Discord Guild ID"),
            ParseId(discordUserId, "Target Discord User ID"),
            Required(steamApiKey, "Steam Web API Key"),
            ParseId(steamId64, "SteamID64"),
            startWithWindows);
    }

    private static string Required(string value, string displayName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException($"{displayName} is required.");
        }

        return value.Trim();
    }

    private static ulong ParseId(string value, string displayName)
    {
        if (!ulong.TryParse(value, out ulong id) || id == 0)
        {
            throw new FormatException($"{displayName} must be a number greater than zero.");
        }

        return id;
    }
}
