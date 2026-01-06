using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using Playnite.SDK;
using Playnite.SDK.Plugins;
using SteamStatusChanger.Models;

namespace SteamStatusChanger
{
    public enum SteamStatus
    {
        Offline,
        Online,
        Away,
        Busy,
        Invisible
    }

    public class SteamStatusChangerPlugin : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static readonly HttpClient httpClient = new HttpClient();

        // Unique plugin ID (don’t change once you start using it)
        public override Guid Id { get; } = Guid.Parse("d7f8c327-14e1-4e06-9d41-bd079f8c7566");

        private TopPanelItem topPanelItem;
        private FrameworkElement statusIconRoot;
        private Ellipse statusDot;
        private Ellipse outerEllipse;
        private SteamStatus currentStatus = SteamStatus.Offline;
        private SteamStatusChangerSettings settings;
        private readonly Timer statusPollTimer;

        public SteamStatusChangerSettings Settings => settings;

        public SteamStatusChangerPlugin(IPlayniteAPI api) : base(api)
        {
            settings = LoadPluginSettings<SteamStatusChangerSettings>() ?? new SteamStatusChangerSettings(this);
            settings.AttachPlugin(this);

            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };

            AddSettingsSupport(new AddSettingsSupportArgs
            {
                SourceName = "SteamStatusChanger",
                SettingsRoot = nameof(Settings)
            });

            statusPollTimer = new Timer { AutoReset = true };
            statusPollTimer.Elapsed += async (s, e) => await PollSteamStatusAsync();
            ApplyTimerSettings();
        }


        public override IEnumerable<TopPanelItem> GetTopPanelItems()
        {
                if (topPanelItem == null)
            {
                statusIconRoot = CreateStatusIcon();

                topPanelItem = new TopPanelItem
                {
                    Title = ResourceProvider.GetString("LOCSteamStatus_TopPanelTitle"),
                    Icon = statusIconRoot,
                    Activated = OnTopPanelItemClicked
                };
            }

            return [topPanelItem];
        }
        public override ISettings GetSettings(bool firstRun)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRun)
        {
            var view = new SettingsControl();
            view.DataContext = settings;
            return view;
        }


        /// <summary>
        /// Called by the settings viewmodel when user hits OK.
        /// </summary>
        internal void OnSettingsSaved(SteamStatusChangerSettings newSettings)
        {
            newSettings?.AttachPlugin(this);
            settings = newSettings ?? new SteamStatusChangerSettings(this);
            SavePluginSettings(settings);
            ApplyTimerSettings();
            // Update UI asynchronously to avoid blocking the settings dialog
            Application.Current?.Dispatcher?.BeginInvoke(new Action(UpdateBorderColor));
        }

        /// <summary>
        /// Update settings reference after they've been saved (called from EndEdit).
        /// </summary>
        internal void UpdateSettingsAfterSave(SteamStatusChangerSettings newSettings)
        {
            newSettings?.AttachPlugin(this);
            settings = newSettings;
            ApplyTimerSettings();
            // Dispatch UI update; timer/poller will run on its own
            Application.Current?.Dispatcher?.BeginInvoke(new Action(UpdateBorderColor));
        }

        private void UpdateBorderColor()
        {
            if (outerEllipse != null && settings != null)
            {
                var borderBrush = settings.BorderBrush ?? Brushes.White;
                
                if (Application.Current?.Dispatcher != null)
                {
                    if (Application.Current.Dispatcher.CheckAccess())
                    {
                        // Already on UI thread
                        outerEllipse.Fill = borderBrush;
                        outerEllipse.Stroke = borderBrush;
                    }
                    else
                    {
                        // Need to switch to UI thread
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            outerEllipse.Fill = borderBrush;
                            outerEllipse.Stroke = borderBrush;
                        }));
                    }
                }
                else
                {
                    outerEllipse.Fill = borderBrush;
                    outerEllipse.Stroke = borderBrush;
                }
            }
        }

        private void ApplyTimerSettings()
        {
            if (settings == null ||
                string.IsNullOrWhiteSpace(settings.ApiKey) ||
                string.IsNullOrWhiteSpace(settings.SteamId))
            {
                statusPollTimer.Enabled = false;
                return;
            }

            var seconds = settings.PollIntervalSeconds;
            if (seconds <= 0)
            {
                seconds = 60;
            }

            statusPollTimer.Interval = seconds * 1000;
            statusPollTimer.Enabled = true;
        }

        #region Top panel icon + menu


        private FrameworkElement CreateStatusIcon()
        {

            // Outer filled white circle (background)
            var borderBrush = settings?.BorderBrush ?? Brushes.White;

            outerEllipse = new Ellipse
            {
                Width = 25,
                Height = 25,
                Fill = borderBrush,
                Stroke = borderBrush,
                StrokeThickness = 1
            };

            // Inner colored dot (status)
            statusDot = new Ellipse
            {
                Width = 15,
                Height = 15,
                Fill = new SolidColorBrush(GetStatusColor(currentStatus)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Stack them perfectly centered
            var grid = new Grid
            {
                Width = 25,
                Height = 25,
                Margin = new Thickness(4, 0, 4, 0)
            };
            grid.Children.Add(outerEllipse);
            grid.Children.Add(statusDot);

            // Tooltip
            grid.ToolTip = string.Format(ResourceProvider.GetString("LOCSteamStatus_SteamStatus_Format"), StatusToText(currentStatus));

            statusIconRoot = grid;
            return grid;
        }


        private void UpdateStatusIcon(SteamStatus status)
        {
            if (statusDot != null)
            {
                statusDot.Fill = new SolidColorBrush(GetStatusColor(status));
            }

            var tooltip = string.Format(ResourceProvider.GetString("LOCSteamStatus_SteamStatus_Format"), StatusToText(status));
            if (statusIconRoot != null)
            {
                statusIconRoot.ToolTip = tooltip;
            }
        }

        private void UpdateIconOnUiThread(SteamStatus status)
        {
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    UpdateStatusIcon(status);
                });
            }
            else
            {
                UpdateStatusIcon(status);
            }
        }

        private string StatusToText(SteamStatus status)
        {
            switch (status)
            {
                case SteamStatus.Online: return ResourceProvider.GetString("LOCSteamStatus_Menu_Online");
                case SteamStatus.Away: return ResourceProvider.GetString("LOCSteamStatus_Menu_Away");
                case SteamStatus.Busy: return ResourceProvider.GetString("LOCSteamStatus_Menu_Busy");
                case SteamStatus.Invisible: return ResourceProvider.GetString("LOCSteamStatus_Menu_Invisible");
                case SteamStatus.Offline:
                default:
                    return ResourceProvider.GetString("LOCSteamStatus_Menu_Offline");
            }
        }

        private Color GetStatusColor(SteamStatus status)
        {
            try
            {
                var s = settings ?? LoadPluginSettings<SteamStatusChangerSettings>() ?? new SteamStatusChangerSettings(this);
                string hex = null;
                switch (status)
                {
                    case SteamStatus.Online: hex = s.OnlineColor; break;
                    case SteamStatus.Away: hex = s.AwayColor; break;
                    case SteamStatus.Busy: hex = s.BusyColor; break;
                    case SteamStatus.Invisible: hex = s.InvisibleColor; break;
                    case SteamStatus.Offline:
                    default: hex = s.OfflineColor; break;
                }

                if (!string.IsNullOrWhiteSpace(hex))
                {
                    return (Color)ColorConverter.ConvertFromString(hex);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ResourceProvider.GetString("LOCSteamStatus_Log_ParseColorError"));
            }

            switch (status)
            {
                case SteamStatus.Online: return Colors.LimeGreen;
                case SteamStatus.Away: return Colors.Goldenrod;
                case SteamStatus.Busy: return Colors.OrangeRed;
                case SteamStatus.Invisible: return Colors.SlateGray;
                case SteamStatus.Offline:
                default: return Colors.DimGray;
            }
        }

        private void OnTopPanelItemClicked()
        {
            try
            {
                var menu = new ContextMenu();

                menu.Items.Add(CreateStatusMenuItem(
                    ResourceProvider.GetString("LOCSteamStatus_Menu_Online"), SteamStatus.Online, GetStatusColor(SteamStatus.Online), "steam://friends/status/online"));
                menu.Items.Add(CreateStatusMenuItem(
                    ResourceProvider.GetString("LOCSteamStatus_Menu_Away"), SteamStatus.Away, GetStatusColor(SteamStatus.Away), "steam://friends/status/away"));
                menu.Items.Add(CreateStatusMenuItem(
                    ResourceProvider.GetString("LOCSteamStatus_Menu_Busy"), SteamStatus.Busy, GetStatusColor(SteamStatus.Busy), "steam://friends/status/busy"));
                menu.Items.Add(CreateStatusMenuItem(
                    ResourceProvider.GetString("LOCSteamStatus_Menu_Invisible"), SteamStatus.Invisible, GetStatusColor(SteamStatus.Invisible), "steam://friends/status/invisible"));

                menu.Items.Add(new Separator());

                menu.Items.Add(CreateStatusMenuItem(
                    ResourceProvider.GetString("LOCSteamStatus_Menu_Offline"), SteamStatus.Offline, GetStatusColor(SteamStatus.Offline), "steam://friends/status/offline"));

                menu.Placement = PlacementMode.MousePoint;
                menu.IsOpen = true;
            }
            catch (Exception)
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    ResourceProvider.GetString("LOCSteamStatus_Error_CouldntOpenMenu"),
                    ResourceProvider.GetString("LOCSteamStatus_Error_Title"));
            }
        }

        private MenuItem CreateStatusMenuItem(string text, SteamStatus status, Color dotColor, string steamUri)
        {
            // Little colored circle
            var dot = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = new SolidColorBrush(dotColor),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };

            // Text label
            var label = new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Lay them out horizontally: [● Online]
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(dot);
            panel.Children.Add(label);

            var item = new MenuItem
            {
                Header = panel
            };

            item.Click += (s, e) => SetSteamStatus(status, steamUri);

            return item;
        }

        private void SetSteamStatus(SteamStatus status, string steamUri)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = steamUri,
                    UseShellExecute = true
                };

                Process.Start(startInfo);

                // Immediately reflect the local choice
                currentStatus = status;
                UpdateIconOnUiThread(currentStatus);
            }
            catch (Exception)
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    ResourceProvider.GetString("LOCSteamStatus_Error_FailedToChange"),
                    ResourceProvider.GetString("LOCSteamStatus_Error_Title"));
            }
        }

        #endregion

        #region Polling / Steam Web API

        private async Task PollSteamStatusAsync()
        {
            try
            {
                if (settings == null ||
                    string.IsNullOrWhiteSpace(settings.ApiKey) ||
                    string.IsNullOrWhiteSpace(settings.SteamId))
                {
                    return;
                }

                var apiStatus = await FetchSteamStatusAsync(settings.ApiKey, settings.SteamId).ConfigureAwait(false);
                if (apiStatus.HasValue && apiStatus.Value != currentStatus)
                {
                    currentStatus = apiStatus.Value;
                    UpdateIconOnUiThread(currentStatus);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ResourceProvider.GetString("LOCSteamStatus_Log_PollError"));
            }
        }

        private int? ExtractPersonaState(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            const string key = "\"personastate\":";
            var index = json.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return null;
            }

            index += key.Length;

            // Skip whitespace
            while (index < json.Length && char.IsWhiteSpace(json[index]))
            {
                index++;
            }

            // Read digits
            var start = index;
            while (index < json.Length && char.IsDigit(json[index]))
            {
                index++;
            }

            if (start == index)
            {
                return null;
            }

            var numberString = json.Substring(start, index - start);

            if (int.TryParse(numberString, out var result))
            {
                return result;
            }

            return null;
        }

        private async Task<SteamStatus?> FetchSteamStatusAsync(string apiKey, string steamId)
        {
            try
            {
                var url = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={apiKey}&steamids={steamId}";
                var json = await httpClient.GetStringAsync(url).ConfigureAwait(false);

                // We only care about the first "personastate" value in the JSON.
                var persona = ExtractPersonaState(json);
                if (!persona.HasValue)
                {
                    return null;
                }

                return PersonaToStatus(persona.Value);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ResourceProvider.GetString("LOCSteamStatus_Log_FetchError"));
                return null;
            }
        }

        private SteamStatus PersonaToStatus(int personaState)
        {
            // Steam persona states:
            // 0 = offline, 1 = online, 2 = busy, 3 = away, 4 = snooze, 5 = looking to trade, 6 = looking to play
            switch (personaState)
            {
                case 1:
                    return SteamStatus.Online;
                case 2:
                    return SteamStatus.Busy;
                case 3:
                case 4:
                    return SteamStatus.Away;
                default:
                    // 0, 5, 6 → treating as Offline in this simple mapping
                    return SteamStatus.Offline;
            }
        }

        #endregion
    }
}
