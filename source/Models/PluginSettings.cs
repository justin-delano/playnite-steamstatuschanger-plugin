using Playnite.SDK;
using Playnite.SDK.Data;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace SteamStatusChanger.Models
{
    public class SteamStatusChangerSettings : ObservableObject, ISettings
    {
        private SteamStatusChangerPlugin plugin;
        private SteamStatusChangerSettings editingClone;

        private string apiKey = string.Empty;
        public string ApiKey
        {
            get => apiKey;
            set => SetValue(ref apiKey, value);
        }

        private string steamId = string.Empty;
        public string SteamId
        {
            get => steamId;
            set => SetValue(ref steamId, value);
        }

        private int pollIntervalSeconds = 60;
        public int PollIntervalSeconds
        {
            get => pollIntervalSeconds;
            set => SetValue(ref pollIntervalSeconds, value);
        }

        private string onlineColor = "#FF32CD32"; // LimeGreen
        public string OnlineColor
        {
            get => onlineColor;
            set => SetValue(ref onlineColor, value);
        }

        private string awayColor = "#FFDAA520"; // Goldenrod
        public string AwayColor
        {
            get => awayColor;
            set => SetValue(ref awayColor, value);
        }

        private string busyColor = "#FFFF4500"; // OrangeRed
        public string BusyColor
        {
            get => busyColor;
            set => SetValue(ref busyColor, value);
        }

        private string invisibleColor = "#FF708090"; // SlateGray
        public string InvisibleColor
        {
            get => invisibleColor;
            set => SetValue(ref invisibleColor, value);
        }

        private string offlineColor = "#FF696969"; // DimGray
        public string OfflineColor
        {
            get => offlineColor;
            set => SetValue(ref offlineColor, value);
        }

        private string borderColor;
        public string BorderColor
        {
            get => borderColor;
            set
            {
                SetValue(ref borderColor, value);
                cachedBorderBrush = null; // Invalidate cache
                OnPropertyChanged(nameof(BorderBrush));
            }
        }

        private Brush cachedBorderBrush;
        
        [DontSerialize]
        public Brush BorderBrush
        {
            get
            {
                if (cachedBorderBrush != null)
                    return cachedBorderBrush;

                cachedBorderBrush = CreateBorderBrush();
                return cachedBorderBrush;
            }
        }

        private Brush CreateBorderBrush()
        {
            // Try to use the user-specified color first
            if (!string.IsNullOrWhiteSpace(BorderColor))
            {
                try
                {
                    var col = (Color)ColorConverter.ConvertFromString(BorderColor);
                    return new SolidColorBrush(col);
                }
                catch { }
            }

            // Fallback to GlyphBrush from theme
            return GetGlyphBrushFromTheme() ?? Brushes.White;
        }

        private Brush GetGlyphBrushFromTheme()
        {
            try
            {
                var app = Application.Current;
                if (app?.Resources != null && app.Resources.Contains("GlyphBrush"))
                {
                    var val = app.Resources["GlyphBrush"];
                    if (val is Brush b) return b;
                    if (val is Color c) return new SolidColorBrush(c);
                }
            }
            catch { }
            return null;
        }

        // Parameterless ctor for deserialization
        public SteamStatusChangerSettings()
        {
            // Ensure BorderColor always has a value
            if (string.IsNullOrWhiteSpace(BorderColor))
            {
                BorderColor = GetGlyphBrushAsHex() ?? "#FFFFFFFF";
            }
        }

        public SteamStatusChangerSettings(SteamStatusChangerPlugin plugin)
        {
            this.plugin = plugin;
            var saved = plugin?.LoadPluginSettings<SteamStatusChangerSettings>();
            if (saved != null)
            {
                ApiKey = saved.ApiKey;
                SteamId = saved.SteamId;
                PollIntervalSeconds = saved.PollIntervalSeconds;
                OnlineColor = saved.OnlineColor;
                AwayColor = saved.AwayColor;
                BusyColor = saved.BusyColor;
                InvisibleColor = saved.InvisibleColor;
                OfflineColor = saved.OfflineColor;
                
                // Only use saved BorderColor if it exists, otherwise calculate default
                if (!string.IsNullOrWhiteSpace(saved.BorderColor))
                {
                    BorderColor = saved.BorderColor;
                }
                else
                {
                    BorderColor = GetGlyphBrushAsHex() ?? "#FFFFFFFF";
                }
            }
            else
            {
                // No saved settings, use default
                BorderColor = GetGlyphBrushAsHex() ?? "#FFFFFFFF";
            }
        }

        internal void AttachPlugin(SteamStatusChangerPlugin plugin)
        {
            this.plugin = plugin;
        }

        private string GetGlyphBrushAsHex()
        {
            var brush = GetGlyphBrushFromTheme();
            if (brush is SolidColorBrush sb)
            {
                var c = sb.Color;
                return $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            }
            return null;
        }

        public void BeginEdit()
        {
            editingClone = Serialization.GetClone(this);
        }

        public void CancelEdit()
        {
            if (editingClone != null)
            {
                ApiKey = editingClone.ApiKey;
                SteamId = editingClone.SteamId;
                PollIntervalSeconds = editingClone.PollIntervalSeconds;
                OnlineColor = editingClone.OnlineColor;
                AwayColor = editingClone.AwayColor;
                BusyColor = editingClone.BusyColor;
                InvisibleColor = editingClone.InvisibleColor;
                OfflineColor = editingClone.OfflineColor;
                BorderColor = editingClone.BorderColor;
            }
        }

        public void EndEdit()
        {
            if (plugin != null)
            {
                plugin.SavePluginSettings(this);
                
                // Update plugin's settings reference and UI
                if (plugin is SteamStatusChangerPlugin p)
                {
                    p.UpdateSettingsAfterSave(this);
                }
            }
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();

            if (!string.IsNullOrWhiteSpace(SteamId) &&
                !ulong.TryParse(SteamId, out _))
            {
                errors.Add(ResourceProvider.GetString("LOCSteamStatus_Verify_SteamIdNumeric"));
            }

            if (PollIntervalSeconds <= 0)
            {
                errors.Add(ResourceProvider.GetString("LOCSteamStatus_Verify_PollIntervalPositive"));
            }

            return errors.Count == 0;
        }
    }
}
