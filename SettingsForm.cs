using System.ComponentModel;
using System.Diagnostics;

namespace DiscordSteamTray;

internal sealed class SettingsForm : Form
{
    private readonly TextBox _discordToken = new() { UseSystemPasswordChar = true };
    private readonly TextBox _guildId = new();
    private readonly TextBox _userId = new();
    private readonly TextBox _steamApiKey = new() { UseSystemPasswordChar = true };
    private readonly TextBox _steamId64 = new();
    private readonly CheckBox _startWithWindows = new()
    {
        Text = "Start automatically when I sign in to Windows",
        AutoSize = true
    };
    private readonly Label _errorLabel = new()
    {
        AutoSize = true,
        ForeColor = Color.Firebrick
    };

    public TraySettings Settings { get; private set; } = null!;

    public SettingsForm(TraySettings? currentSettings)
    {
        Text = "Discord Steam Tray - Settings";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(480, 420);

        if (currentSettings is not null)
        {
            _discordToken.Text = currentSettings.DiscordBotToken;
            _guildId.Text = currentSettings.DiscordGuildId.ToString();
            _userId.Text = currentSettings.DiscordUserId.ToString();
            _steamApiKey.Text = currentSettings.SteamApiKey;
            _steamId64.Text = currentSettings.SteamId64.ToString();
            _startWithWindows.Checked = currentSettings.StartWithWindows;
        }

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 11,
            Padding = new Padding(12),
            AutoSize = false
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int row = 0; row < 5; row++)
        {
            fields.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
        }
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        AddField(fields, 0, "Discord Bot Token", _discordToken);
        AddField(fields, 1, "Discord Guild ID", _guildId);
        AddField(fields, 2, "Target Discord User ID", _userId);
        AddField(fields, 3, "Steam Web API Key", _steamApiKey);
        AddField(fields, 4, "SteamID64", _steamId64);
        fields.Controls.Add(_startWithWindows, 0, 5);
        fields.SetColumnSpan(_startWithWindows, 2);
        fields.Controls.Add(CreateHelpLink(
            "Open Discord Developer Portal (Bot Token)",
            "https://discord.com/developers/applications"), 0, 6);
        fields.SetColumnSpan(fields.GetControlFromPosition(0, 6)!, 2);
        fields.Controls.Add(CreateHelpLink(
            "Open Steam Web API Key page",
            "https://steamcommunity.com/dev/apikey"), 0, 7);
        fields.SetColumnSpan(fields.GetControlFromPosition(0, 7)!, 2);
        var instructionsLink = new LinkLabel
        {
            Text = "How to find these values",
            AutoSize = true,
            Anchor = AnchorStyles.Left
        };
        instructionsLink.LinkClicked += (_, _) =>
        {
            using var helpForm = new SettingsHelpForm();
            helpForm.ShowDialog(this);
        };
        fields.Controls.Add(instructionsLink, 0, 8);
        fields.SetColumnSpan(instructionsLink, 2);
        fields.Controls.Add(_errorLabel, 0, 9);
        fields.SetColumnSpan(_errorLabel, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };
        var saveButton = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.None,
            AutoSize = true
        };
        saveButton.Click += SaveButton_Click;
        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            AutoSize = true
        };
        buttons.Controls.Add(saveButton);
        buttons.Controls.Add(cancelButton);
        fields.Controls.Add(buttons, 0, 10);
        fields.SetColumnSpan(buttons, 2);

        Controls.Add(fields);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private static LinkLabel CreateHelpLink(string text, string url)
    {
        var link = new LinkLabel
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left
        };
        link.LinkClicked += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception exception) when (exception is Win32Exception or
                                               InvalidOperationException or
                                               ArgumentException)
            {
                MessageBox.Show(
                    $"Could not open the page in a browser.\n{exception.Message}",
                    "Discord Steam Tray",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        };
        return link;
    }

    private static void AddField(TableLayoutPanel panel, int row, string label, Control input)
    {
        panel.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, row);
        input.Dock = DockStyle.Fill;
        input.Margin = new Padding(3, 5, 3, 5);
        panel.Controls.Add(input, 1, row);
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        try
        {
            Settings = TraySettings.Create(
                _discordToken.Text,
                _guildId.Text,
                _userId.Text,
                _steamApiKey.Text,
                _steamId64.Text,
                _startWithWindows.Checked);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (FormatException exception)
        {
            _errorLabel.Text = exception.Message;
        }
    }
}
