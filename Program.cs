using DiscordSteamTray;
using System.Security.Cryptography;
using System.Text.Json;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        try
        {
            TraySettings? settings = SettingsStore.Load();
            if (settings is null)
            {
                using var settingsForm = new SettingsForm(currentSettings: null);
                if (settingsForm.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                settings = settingsForm.Settings;
                SettingsStore.Save(settings);
            }

            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            Application.Run(new TrayApplicationContext(settings, SynchronizationContext.Current!));
        }
        catch (Exception exception) when (exception is InvalidOperationException or
                                           FormatException or
                                           IOException or
                                           JsonException or
                                           CryptographicException)
        {
            MessageBox.Show(
                exception.Message,
                "Discord Steam Tray - Settings Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
