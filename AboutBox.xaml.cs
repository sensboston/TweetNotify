using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

namespace TweetNotify
{
    public partial class AboutBox : Window
    {
        public AboutBox()
        {
            InitializeComponent();
            LoadAssemblyInfo();
        }

        private void LoadAssemblyInfo()
        {
            var assembly = Assembly.GetExecutingAssembly();

            // Retrieve title from [assembly: AssemblyTitle("...")]
            var titleAttribute = assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? "";

            // Retrieve version from AssemblyVersion
            var versionInfo = assembly.GetName().Version?.ToString() ?? "Unknown Version";

            // Additional attributes
            var descriptionAttribute = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? "";
            var copyrightAttribute = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "";

            AppNameTextBlock.Text = titleAttribute;
            AppVersionTextBlock.Text = $"Version: {versionInfo}";
            AssemblyDescriptionTextBlock.Text = descriptionAttribute;
            AssemblyCopyrightTextBlock.Text = copyrightAttribute;
        }


        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}
