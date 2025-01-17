using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Navigation;
using MarkdownSharp;

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
            var titleAttribute = assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? "";
            var versionInfo = assembly.GetName().Version?.ToString() ?? "Unknown Version";
            var descriptionAttribute = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? "";
            var copyrightAttribute = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "";

            AppNameTextBlock.Text = titleAttribute;
            AppVersionTextBlock.Text = $"Version: {versionInfo}";
            AssemblyDescriptionTextBlock.Text = descriptionAttribute;
            AssemblyCopyrightTextBlock.Text = copyrightAttribute;
        }

        private void SetupInstructions_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            // Load README.MD from resources
            string markdownText;
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TweetNotify.README.md"))
            using (var reader = new StreamReader(stream))
                markdownText = reader.ReadToEnd();

            // Convert GitHub markdown to html
            var markdown = new Markdown();
            string htmlContent = head + css + markdown.Transform(markdownText) + close;

            // Fix images urls
            htmlContent = htmlContent.Replace("https://github.com/user-attachments/assets/8d4230d6-8344-431f-8084-1fada38c8441", "https://senssoft.com/tn1.png");
            htmlContent = htmlContent.Replace("https://github.com/user-attachments/assets/d995d22d-e63d-4383-971c-a9acd01d59a4", "https://senssoft.com/tn2.png");

            string tempFile = Path.Combine(Path.GetTempPath(), "README_Temp.html");
            File.WriteAllText(tempFile, htmlContent, Encoding.UTF8);

            // Show created page in browser
            Process.Start(new ProcessStartInfo(tempFile) { UseShellExecute = true });

            Close();
        }

        string head = $"<html><head><meta charset='utf-8'/><title>README</title>{css}</head><body>";
        const string close = "</body><html>";
        // GitHub style for README.MD
        const string css =
@"<style>body{color:#24292e;background-color:#ffffff;font-family:-apple-system,BlinkMacSystemFont,
""Segoe UI"",Helvetica,Arial,sans-serif,""Apple Color Emoji"",""Segoe UI Emoji"";
line-height:1.5;margin:1em auto;max-width:800px;padding:0 1em;}h1,h2,h3,h4,h5,h6{font-weight:600;
margin-top:1.5em;margin-bottom:0.75em;line-height:1.25;}h1{font-size:2em;border-bottom:1px solid #eaecef;
padding-bottom:0.3em;}h2{font-size:1.5em;border-bottom:1px solid #eaecef;padding-bottom:0.3em;}
h3{font-size:1.25em;}h4,h5,h6{font-size:1em;}p{margin-top:0;margin-bottom:0.75em;}a{color:#0366d6;text-decoration:none;}
a:hover{text-decoration:underline;}ul,ol{margin-top:0;margin-bottom:0.75em;padding-left:2em;}
li{margin-bottom:0.5em;}code{background-color:#f6f8fa;color:#24292e;padding:0.2em 0.4em;font-size:0.9em;
border-radius:3px;font-family:Consolas,""Liberation Mono"",Menlo,Courier,monospace;}
hr{border:0;border-top:1px solid #eaecef;margin:1.5em 0;}strong{font-weight:600;}em{font-style:italic;}</style>";
    }
}
