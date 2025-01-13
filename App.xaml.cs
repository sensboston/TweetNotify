using System.Windows;
using Bluegrams.Application;
using ModernWpf;
using TweetNotify.Properties;

namespace TweetNotify
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Setup portable settings provider
            PortableSettingsProvider.SettingsFileName = "TweetNotify.config";
            PortableSettingsProvider.ApplyProvider(Settings.Default);

            // Continue with the application startup
            base.OnStartup(e);

            ThemeManager.Current.ApplicationTheme = Settings.Default.Theme == "Dark" ? ApplicationTheme.Dark : ApplicationTheme.Light;
        }
    }
}
