using System.Windows.Controls;
using System.Windows.Media;
using SteamStatusChanger.Common;
using SteamStatusChanger.Models;

namespace SteamStatusChanger
{
    public partial class SettingsControl : UserControl
    {
        public SettingsControl()
        {
            InitializeComponent();
        }

        private void OnPickColor(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!(sender is Button b) || b.Tag == null) return;

            var key = b.Tag.ToString();

            // DataContext is SteamStatusChangerSettings
            if (!(DataContext is SteamStatusChangerSettings settings)) return;
            using (var dlg = new System.Windows.Forms.ColorDialog())
            {
                // Try to initialize with existing color
                string initial = null;
                switch (key)
                {
                    case "Online": initial = settings.OnlineColor; break;
                    case "Away": initial = settings.AwayColor; break;
                    case "Busy": initial = settings.BusyColor; break;
                    case "Invisible": initial = settings.InvisibleColor; break;
                    case "Offline": initial = settings.OfflineColor; break;
                    case "Border": initial = settings.BorderColor; break;
                }

                try
                {
                    if (!string.IsNullOrWhiteSpace(initial))
                    {
                        var c = (Color)ColorConverter.ConvertFromString(initial);
                        dlg.Color = System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
                    }
                }
                catch { }

                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var sc = dlg.Color;
                    var hex = $"#{sc.A:X2}{sc.R:X2}{sc.G:X2}{sc.B:X2}";
                    switch (key)
                    {
                        case "Online": settings.OnlineColor = hex; break;
                        case "Away": settings.AwayColor = hex; break;
                        case "Busy": settings.BusyColor = hex; break;
                        case "Invisible": settings.InvisibleColor = hex; break;
                        case "Offline": settings.OfflineColor = hex; break;
                        case "Border": settings.BorderColor = hex; break;
                    }
                }
            }
        }
    }
}