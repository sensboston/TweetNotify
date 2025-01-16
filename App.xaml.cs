using System.Threading;
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
        private static Mutex mutex = null;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string appName = "TweetNotify";
            bool createdNew;
            mutex = new Mutex(true, appName, out createdNew);
            if (!createdNew)
            {
                // If an instance is already running, shut down this new one.
                Current.Shutdown();
                return;
            }

            // Setup portable settings provider
            PortableSettingsProvider.SettingsFileName = "TweetNotify.config";
            PortableSettingsProvider.ApplyProvider(Settings.Default);

            // Continue with the application startup
            base.OnStartup(e);

            // Set app theme
            ThemeManager.Current.ApplicationTheme = Settings.Default.Theme == "Dark" ? ApplicationTheme.Dark : ApplicationTheme.Light;
        }
    }
}
