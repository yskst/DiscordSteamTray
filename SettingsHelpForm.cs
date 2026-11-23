namespace DiscordSteamTray;

internal sealed class SettingsHelpForm : Form
{
    public SettingsHelpForm()
    {
        Text = "How to find your settings values";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(600, 460);

        var instructions = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            WordWrap = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Text = string.Join(
                Environment.NewLine,
                "DISCORD BOT TOKEN",
                "1. Open the Discord Developer Portal using the link in the settings window.",
                "2. Create an application and select Bot. Click Add Bot if prompted.",
                "3. Use Reset Token or View Token on the Bot page, then copy the token.",
                "4. On the Bot page, enable Presence Intent and Server Members Intent.",
                "",
                "INVITE THE BOT TO YOUR SERVER",
                "1. In the Discord Developer Portal, select your application and open OAuth2 > URL Generator.",
                "2. Select the bot scope. No bot permissions are required for presence monitoring.",
                "3. Open the generated URL, choose the target server, and authorize the bot.",
                "4. Your Discord account needs Manage Server permission. Invite it to the same server as the target user.",
                "",
                "DISCORD GUILD ID AND TARGET USER ID",
                "1. In Discord, open User Settings > Advanced and enable Developer Mode.",
                "2. Right-click the target server and select Copy Server ID.",
                "3. Right-click the target user and select Copy User ID.",
                "4. The bot must be a member of the server containing the target user.",
                "",
                "STEAM WEB API KEY",
                "1. Open the Steam Web API Key page using the link in the settings window and sign in.",
                "2. Register a key and copy it into the Steam Web API Key field.",
                "3. For Domain Name, enter localhost. This app runs locally and does not need a website.",
                "",
                "STEAMID64",
                "1. Open the target Steam profile and inspect its URL.",
                "2. For a URL like steamcommunity.com/profiles/765..., the number after /profiles/ is the SteamID64.",
                "3. A URL using /id/custom-name/ contains a vanity name, not a SteamID64. Resolve it with Steam's ISteamUser/ResolveVanityURL API method.",
                "",
                "Keep your Discord bot token and Steam Web API key private.")
        };

        var closeButton = new Button
        {
            Text = "Close",
            DialogResult = DialogResult.OK,
            AutoSize = true
        };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };
        buttons.Controls.Add(closeButton);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.Controls.Add(instructions, 0, 0);
        layout.Controls.Add(buttons, 0, 1);
        Controls.Add(layout);

        AcceptButton = closeButton;
        CancelButton = closeButton;
    }
}
