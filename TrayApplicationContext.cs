using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Net.WebSockets;
using System.Text.Json;
using Discord;
using Discord.WebSocket;
using DrawingColor = System.Drawing.Color;

namespace DiscordSteamTray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private static readonly DrawingColor OnlineColor = ColorTranslator.FromHtml("#23A55A");
    private static readonly DrawingColor AwayColor = ColorTranslator.FromHtml("#F0B232");
    private static readonly DrawingColor BusyColor = ColorTranslator.FromHtml("#F23F43");
    private static readonly DrawingColor OfflineColor = ColorTranslator.FromHtml("#80848E");
    private static readonly TimeSpan SteamPollInterval = TimeSpan.FromSeconds(30);

    private readonly TraySettings _settings;
    private readonly SynchronizationContext _uiContext;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _refreshMenuItem;
    private readonly HttpClient _httpClient = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly SemaphoreSlim _steamRefreshLock = new(1, 1);
    private DiscordSocketClient? _discordClient;
    private Icon? _currentIcon;
    private Task? _steamPollingTask;
    private Task? _discordStartupTask;
    private UserStatus _discordStatus = UserStatus.Offline;
    private SteamStatus _steamStatus = SteamStatus.Unknown;
    private bool _targetIsGuildMember;
    private bool _isExiting;

    public TrayApplicationContext(TraySettings settings, SynchronizationContext uiContext)
    {
        _settings = settings;
        _uiContext = uiContext;

        var menu = new ContextMenuStrip();
        _refreshMenuItem = new ToolStripMenuItem("Refresh now");
        _refreshMenuItem.Click += async (_, _) => await RefreshNowAsync();
        menu.Items.Add(_refreshMenuItem);
        menu.Items.Add("Settings...", null, async (_, _) => await ConfigureAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, async (_, _) => await ExitAsync());

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Text = "Discord: Offline / Steam: Unknown",
            Visible = true
        };
        UpdateTrayDisplay();

        _steamPollingTask = PollSteamAsync(_shutdown.Token);
        _discordStartupTask = StartDiscordAsync();
    }

    private async Task ConfigureAsync()
    {
        if (_isExiting)
        {
            return;
        }

        using var settingsForm = new SettingsForm(_settings);
        if (settingsForm.ShowDialog() != DialogResult.OK ||
            settingsForm.Settings == _settings)
        {
            return;
        }

        try
        {
            SettingsStore.Save(settingsForm.Settings);
            StartupManager.SetEnabled(settingsForm.Settings.StartWithWindows);
            string executablePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("Could not determine the application executable path.");
            Process? restartedProcess = Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = true
            });
            if (restartedProcess is null)
            {
                throw new InvalidOperationException("The application could not be restarted.");
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or
                                           IOException or
                                           UnauthorizedAccessException or
                                           System.Security.SecurityException or
                                           System.Security.Cryptography.CryptographicException or
                                           System.ComponentModel.Win32Exception)
        {
            Trace.TraceError("Unable to save settings or restart application: {0}", exception);
            MessageBox.Show(
                $"Could not apply settings or restart the application.\n{exception.Message}",
                "Discord Steam Tray - Settings Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        await ExitAsync();
    }

    private async Task StartDiscordAsync()
    {
        try
        {
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds |
                                 GatewayIntents.GuildPresences |
                                 GatewayIntents.GuildMembers
            };
            _discordClient = new DiscordSocketClient(config);
            _discordClient.Ready += OnDiscordReadyAsync;
            _discordClient.PresenceUpdated += OnPresenceUpdatedAsync;
            await _discordClient.LoginAsync(TokenType.Bot, _settings.DiscordBotToken);
            await _discordClient.StartAsync();
        }
        catch (Exception exception) when (exception is Discord.Net.HttpException or
                                           SocketException or
                                           TaskCanceledException or
                                           WebSocketException or
                                           InvalidOperationException)
        {
            Trace.TraceError("Discord connection failed: {0}", exception);
            PostToUi(() =>
            {
                if (!_isExiting)
                {
                    _discordStatus = UserStatus.Offline;
                    UpdateTrayDisplay();
                    _notifyIcon.ShowBalloonTip(
                        5000,
                        "Discord connection error",
                        exception.Message,
                        ToolTipIcon.Error);
                }
            });
        }
    }

    private async Task OnDiscordReadyAsync()
    {
        try
        {
            await RefreshDiscordAsync();
        }
        catch (Discord.Net.HttpException exception)
        {
            Trace.TraceError("Discord member lookup failed: {0}", exception);
            PostDiscordStatus(UserStatus.Offline);
            PostToUi(() =>
            {
                if (!_isExiting)
                {
                    _notifyIcon.ShowBalloonTip(
                        5000,
                        "Discord user lookup error",
                        exception.Message,
                        ToolTipIcon.Error);
                }
            });
        }
    }

    private Task OnPresenceUpdatedAsync(SocketUser user, SocketPresence _, SocketPresence presence)
    {
        if (user.Id == _settings.DiscordUserId && _targetIsGuildMember)
        {
            UserStatus status = presence.Status;
            PostToUi(() =>
            {
                if (!_isExiting)
                {
                    _discordStatus = status;
                    UpdateTrayDisplay();
                }
            });
        }

        return Task.CompletedTask;
    }

    private async Task RefreshDiscordAsync()
    {
        DiscordSocketClient? client = _discordClient;
        if (client is null)
        {
            return;
        }

        SocketGuild? guild = client.GetGuild(_settings.DiscordGuildId);
        if (guild is null)
        {
            _targetIsGuildMember = false;
            PostDiscordStatus(UserStatus.Offline);
            return;
        }

        SocketGuildUser? cachedMember = guild.GetUser(_settings.DiscordUserId);
        if (cachedMember is not null)
        {
            _targetIsGuildMember = true;
            PostDiscordStatus(cachedMember.Status);
            return;
        }

        IGuildUser? guildMember = await ((IGuild)guild).GetUserAsync(
            _settings.DiscordUserId,
            CacheMode.AllowDownload);
        _targetIsGuildMember = guildMember is not null;
        PostDiscordStatus(client.GetUser(_settings.DiscordUserId)?.Status ?? UserStatus.Offline);
    }

    private async Task PollSteamAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(SteamPollInterval);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RefreshSteamAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (exception is HttpRequestException or
                                               TaskCanceledException or
                                               JsonException or
                                               InvalidOperationException)
            {
                Trace.TraceError("Steam status update failed: {0}", exception);
                PostSteamStatus(SteamStatus.Unknown);
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(cancellationToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RefreshSteamAsync(CancellationToken cancellationToken)
    {
        await _steamRefreshLock.WaitAsync(cancellationToken);
        try
        {
            string url = "https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/" +
                         $"?key={Uri.EscapeDataString(_settings.SteamApiKey)}" +
                         $"&steamids={_settings.SteamId64}";
            using HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty("response", out JsonElement responseElement) ||
                !responseElement.TryGetProperty("players", out JsonElement players) ||
                players.ValueKind != JsonValueKind.Array ||
                players.GetArrayLength() == 0 ||
                !players[0].TryGetProperty("personastate", out JsonElement personaState) ||
                !personaState.TryGetInt32(out int state))
            {
                throw new InvalidOperationException("The Steam API response did not include a status for the target player.");
            }

            PostSteamStatus(MapSteamStatus(state));
        }
        finally
        {
            _steamRefreshLock.Release();
        }
    }

    private async Task RefreshNowAsync()
    {
        if (_isExiting)
        {
            return;
        }

        _refreshMenuItem.Enabled = false;
        try
        {
            await RefreshDiscordAsync();
            await RefreshSteamAsync(_shutdown.Token);
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is HttpRequestException or
                                           TaskCanceledException or
                                           JsonException or
                                           InvalidOperationException or
                                           Discord.Net.HttpException)
        {
            Trace.TraceError("Manual status refresh failed: {0}", exception);
            _notifyIcon.ShowBalloonTip(
                5000,
                "Status update error",
                exception.Message,
                ToolTipIcon.Error);
        }
        finally
        {
            if (!_isExiting)
            {
                _refreshMenuItem.Enabled = true;
            }
        }
    }

    private void PostDiscordStatus(UserStatus status)
    {
        PostToUi(() =>
        {
            if (!_isExiting)
            {
                _discordStatus = status;
                UpdateTrayDisplay();
            }
        });
    }

    private void PostSteamStatus(SteamStatus status)
    {
        PostToUi(() =>
        {
            if (!_isExiting)
            {
                _steamStatus = status;
                UpdateTrayDisplay();
            }
        });
    }

    private void PostToUi(Action action)
    {
        _uiContext.Post(_ => action(), null);
    }

    private void UpdateTrayDisplay()
    {
        string discordText = FormatDiscordStatus(_discordStatus);
        string steamText = FormatSteamStatus(_steamStatus);
        _notifyIcon.Text = $"Discord: {discordText} / Steam: {steamText}";

        Icon nextIcon = CreateStatusIcon(GetDiscordColor(_discordStatus), GetSteamColor(_steamStatus));
        Icon? previousIcon = _currentIcon;
        _currentIcon = nextIcon;
        _notifyIcon.Icon = nextIcon;
        previousIcon?.Dispose();
    }

    private static Icon CreateStatusIcon(DrawingColor discordColor, DrawingColor steamColor)
    {
        using var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(DrawingColor.Transparent);

            var tileBounds = new RectangleF(1, 1, 30, 30);
            using GraphicsPath tilePath = CreateRoundedRectanglePath(tileBounds, 7);
            using var tileBrush = new LinearGradientBrush(
                tileBounds,
                DrawingColor.FromArgb(42, 49, 66),
                DrawingColor.FromArgb(17, 21, 31),
                LinearGradientMode.Vertical);
            using var tileOutline = new Pen(DrawingColor.FromArgb(210, 146, 158, 180), 1);
            graphics.FillPath(tileBrush, tilePath);
            graphics.DrawPath(tileOutline, tilePath);

            DrawStatusPanel(graphics, new RectangleF(4, 4, 10.5f, 24), discordColor);
            DrawStatusPanel(graphics, new RectangleF(17.5f, 4, 10.5f, 24), steamColor);
        }

        IntPtr iconHandle = bitmap.GetHicon();
        try
        {
            using Icon temporaryIcon = Icon.FromHandle(iconHandle);
            return (Icon)temporaryIcon.Clone();
        }
        finally
        {
            DestroyIcon(iconHandle);
        }
    }

    private static void DrawStatusPanel(Graphics graphics, RectangleF bounds, DrawingColor statusColor)
    {
        using GraphicsPath panelPath = CreateRoundedRectanglePath(bounds, 4);
        using var panelBrush = new LinearGradientBrush(
            bounds,
            ControlPaint.Light(statusColor, 0.25f),
            ControlPaint.Dark(statusColor, 0.2f),
            LinearGradientMode.Vertical);
        using var panelOutline = new Pen(DrawingColor.FromArgb(190, 255, 255, 255), 0.75f);
        using var indicatorBrush = new SolidBrush(DrawingColor.FromArgb(245, 255, 255, 255));

        graphics.FillPath(panelBrush, panelPath);
        graphics.DrawPath(panelOutline, panelPath);
        graphics.FillEllipse(
            indicatorBrush,
            bounds.X + (bounds.Width - 3.5f) / 2,
            bounds.Y + (bounds.Height - 3.5f) / 2,
            3.5f,
            3.5f);
    }

    private static GraphicsPath CreateRoundedRectanglePath(RectangleF bounds, float radius)
    {
        float diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static DrawingColor GetDiscordColor(UserStatus status) => status switch
    {
        UserStatus.Online => OnlineColor,
        UserStatus.Idle => AwayColor,
        UserStatus.DoNotDisturb => BusyColor,
        _ => OfflineColor
    };

    private static DrawingColor GetSteamColor(SteamStatus status) => status switch
    {
        SteamStatus.Online => OnlineColor,
        SteamStatus.Away => AwayColor,
        SteamStatus.Busy => BusyColor,
        _ => OfflineColor
    };

    private static string FormatDiscordStatus(UserStatus status) => status switch
    {
        UserStatus.Online => "Online",
        UserStatus.Idle => "Idle",
        UserStatus.DoNotDisturb => "Do Not Disturb",
        _ => "Offline"
    };

    private static string FormatSteamStatus(SteamStatus status) => status switch
    {
        SteamStatus.Online => "Online",
        SteamStatus.Away => "Away",
        SteamStatus.Busy => "Busy",
        SteamStatus.Offline => "Offline",
        _ => "Unknown"
    };

    private static SteamStatus MapSteamStatus(int state) => state switch
    {
        0 => SteamStatus.Offline,
        1 => SteamStatus.Online,
        2 => SteamStatus.Busy,
        3 or 4 => SteamStatus.Away,
        5 or 6 => SteamStatus.Online,
        _ => SteamStatus.Offline
    };

    private async Task ExitAsync()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        _notifyIcon.Visible = false;
        _shutdown.Cancel();
        Exception? shutdownError = null;

        try
        {
            if (_steamPollingTask is not null)
            {
                await _steamPollingTask;
            }

            if (_discordStartupTask is not null)
            {
                await _discordStartupTask;
            }

            if (_discordClient is not null)
            {
                _discordClient.PresenceUpdated -= OnPresenceUpdatedAsync;
                _discordClient.Ready -= OnDiscordReadyAsync;
                await _discordClient.StopAsync();
                await _discordClient.LogoutAsync();
            }
        }
        catch (Exception exception) when (exception is Discord.Net.HttpException or
                                           SocketException or
                                           TaskCanceledException or
                                           WebSocketException or
                                           InvalidOperationException)
        {
            shutdownError = exception;
            Trace.TraceError("Error while stopping services: {0}", exception);
        }
        finally
        {
            try
            {
                if (_discordClient is not null)
                {
                    await _discordClient.DisposeAsync();
                }
            }
            catch (Exception exception) when (exception is Discord.Net.HttpException or
                                               SocketException or
                                               TaskCanceledException or
                                               WebSocketException or
                                               InvalidOperationException)
            {
                shutdownError ??= exception;
                Trace.TraceError("Error while disposing Discord client: {0}", exception);
            }

            _httpClient.Dispose();
            _steamRefreshLock.Dispose();
            _shutdown.Dispose();
            _currentIcon?.Dispose();
            _currentIcon = null;
            _notifyIcon.Dispose();
            ExitThread();
        }

        if (shutdownError is not null)
        {
            MessageBox.Show(
                $"An error occurred while stopping one or more services.\n{shutdownError.Message}",
                "Discord Steam Tray",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_isExiting)
        {
            _shutdown.Cancel();
            _httpClient.Dispose();
            _steamRefreshLock.Dispose();
            _shutdown.Dispose();
            _currentIcon?.Dispose();
            _notifyIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    private enum SteamStatus
    {
        Unknown,
        Offline,
        Online,
        Away,
        Busy
    }
}
