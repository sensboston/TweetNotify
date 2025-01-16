using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
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


        private void SetupInstructions_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "TweetNotify.README.md";

            if (ResourceExists(Assembly.GetExecutingAssembly(), resourceName))
            {
                string markdownText;
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                using (var reader = new StreamReader(stream))
                    markdownText = reader.ReadToEnd();

                string htmlContent = ConvertMarkdownToHtml(markdownText);
                string tempFile = Path.Combine(Path.GetTempPath(), "README_Temp.html");
                File.WriteAllText(tempFile, htmlContent, Encoding.UTF8);

                Process.Start(new ProcessStartInfo(tempFile) { UseShellExecute = true });
            }
        }

        const string css =
@"body{color:#24292e;background-color:#ffffff;font-family:-apple-system,BlinkMacSystemFont,
""Segoe UI"",Helvetica,Arial,sans-serif,""Apple Color Emoji"",""Segoe UI Emoji"";
line-height:1.5;margin:1em auto;max-width:800px;padding:0 1em;}h1,h2,h3,h4,h5,h6{font-weight:600;
margin-top:1.5em;margin-bottom:0.75em;line-height:1.25;}h1{font-size:2em;border-bottom:1px solid #eaecef;
padding-bottom:0.3em;}h2{font-size:1.5em;border-bottom:1px solid #eaecef;padding-bottom:0.3em;}
h3{font-size:1.25em;}h4,h5,h6{font-size:1em;}p{margin-top:0;margin-bottom:0.75em;}a{color:#0366d6;text-decoration:none;}
a:hover{text-decoration:underline;}ul,ol{margin-top:0;margin-bottom:0.75em;padding-left:2em;}
li{margin-bottom:0.5em;}code{background-color:#f6f8fa;color:#24292e;padding:0.2em 0.4em;font-size:0.9em;
border-radius:3px;font-family:Consolas,""Liberation Mono"",Menlo,Courier,monospace;}
hr{border:0;border-top:1px solid #eaecef;margin:1.5em 0;}strong{font-weight:600;}em{font-style:italic;}";

        private string ConvertMarkdownToHtml(string markdown)
        {
            var lines = markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var sb = new StringBuilder();
            sb.Append($"<html><head><meta charset=\"utf-8\" /><style>{css}</style><title>TweetNotify setup instructions</title></head><body>");

            bool inList = false;
            foreach (var line in lines)
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith("# "))
                {
                    sb.Append("<h1>" + trimmed.Substring(2) + "</h1>");
                }
                else if (trimmed.StartsWith("## "))
                {
                    sb.Append("<h2>" + trimmed.Substring(3) + "</h2>");
                }
                else if (trimmed.StartsWith("### "))
                {
                    sb.Append("<h3>" + trimmed.Substring(4) + "</h3>");
                }
                else if (trimmed.StartsWith("#### "))
                {
                    sb.Append("<h4>" + trimmed.Substring(5) + "</h4>");
                }
                else if (trimmed.StartsWith("1.") || trimmed.StartsWith("2.") || trimmed.StartsWith("3.") ||
                         trimmed.StartsWith("4.") || trimmed.StartsWith("5.") || trimmed.StartsWith("- "))
                {
                    if (!inList)
                    {
                        inList = true;
                        sb.Append("<ul>");
                    }
                    int dotIndex = trimmed.IndexOf('.');
                    if (dotIndex < 0) dotIndex = 1;
                    string itemText = trimmed.Substring(dotIndex + 1).TrimStart(' ', '-');
                    itemText = ReplaceLinks(itemText);
                    sb.Append("<li>" + itemText + "</li>");
                }
                else
                {
                    if (inList)
                    {
                        inList = false;
                        sb.Append("</ul>");
                    }
                    sb.Append("<p>" + ReplaceLinks(trimmed) + "</p>");
                }
            }

            if (inList) sb.Append("</ul>");

            sb.Append("</body></html>");
            return sb.ToString();
        }

        private string ReplaceFormatting(string text)
        {
            text = Regex.Replace(text, @"\*\*(.+?)\*\*", "<strong>$1</strong>"); // Bold: **text**
            text = Regex.Replace(text, @"\*(.+?)\*", "<em>$1</em>"); // Italic: *text*
            return text;
        }

        private string ReplaceLinks(string text)
        {
            int startIndex = 0;
            var sb = new StringBuilder();
            while (true)
            {
                int linkStart = text.IndexOf("[", startIndex);
                if (linkStart == -1)
                {
                    sb.Append(text.Substring(startIndex));
                    break;
                }
                sb.Append(text.Substring(startIndex, linkStart - startIndex));
                int linkEnd = text.IndexOf("]", linkStart);
                if (linkEnd == -1)
                {
                    sb.Append(text.Substring(linkStart));
                    break;
                }
                string linkText = text.Substring(linkStart + 1, linkEnd - linkStart - 1);
                int urlStart = text.IndexOf("(", linkEnd);
                int urlEnd = text.IndexOf(")", urlStart);
                if (urlStart == -1 || urlEnd == -1)
                {
                    sb.Append("[" + linkText + "]");
                    startIndex = linkEnd + 1;
                    continue;
                }
                string url = text.Substring(urlStart + 1, urlEnd - urlStart - 1);
                sb.Append("<a href=\"" + url + "\">" + linkText + "</a>");
                startIndex = urlEnd + 1;
            }
            return sb.ToString();
        }

        private bool ResourceExists(Assembly assembly, string resourceName)
        {
            foreach (var name in assembly.GetManifestResourceNames())
                if (name.Equals(resourceName, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
