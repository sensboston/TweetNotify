using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Speech.Synthesis;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.ComponentModel;

using Newtonsoft.Json;
using NullSoftware.ToolKit;
using PuppeteerSharp;
using TweetNotify.Properties;

namespace TweetNotify
{
    public partial class MainWindow : Window
    {
        private IBrowser browser;
        private DispatcherTimer timer;
        private List<(string handle, string mode)> accounts;
        private Dictionary<string, List<string>> seenTweetIdsByUser = new Dictionary<string, List<string>>();
        SpeechSynthesizer synthesizer;

        public MainWindow()
        {
            InitializeComponent();
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            Hide();

            // Setup speech synthesizer
            synthesizer = new SpeechSynthesizer();
            synthesizer.SetOutputToDefaultAudioDevice();

            // Get the list of available voices
            foreach (var voice in synthesizer.GetInstalledVoices())
            {
                Debug.WriteLine($"- {voice.VoiceInfo.Name} (Gender: {voice.VoiceInfo.Gender}, Age: {voice.VoiceInfo.Age})");
            }

            synthesizer.SelectVoice(Settings.Default.Voice);
            synthesizer.Volume = Settings.Default.Volume;

            // Parse X accounts
            accounts = ParseAccounts(Settings.Default.TwitterAccounts);

            // Initialize list for UI
            var displayList = new List<AccountDisplay>();
            foreach (var (handle, mode) in accounts)
            {
                string fullHandle = "@" + handle;
                displayList.Add(new AccountDisplay
                {
                    TwitterHandle = fullHandle,
                    NotificationMode = mode,
                });
                seenTweetIdsByUser[fullHandle] = new List<string>();
            }
            AccountsList.ItemsSource = displayList;

            Closing += (object sender, CancelEventArgs e) => { e.Cancel = true; Hide(); };

            // Setup Puppeteer
            await SetupPuppeteerAsync();

            // Baseline fetch (to avoid notifications on old tweets)
            await FetchAllFeedsAsync(accounts, true);

            // Setup timer
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(Settings.Default.UpdateTime);
            timer.Tick += async (_, __) => await FetchAllFeedsAsync(accounts, false);
            timer.Start();
        }

        private List<(string handle, string mode)> ParseAccounts(string raw)
        {
            var result = new List<(string handle, string mode)>();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                // Split string to separate accounts
                var items = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string item in items)
                {
                    var parts = item.Split(new[] { ':' }, 2);
                    if (parts.Length == 2)
                    {
                        result.Add((parts[0].Trim().TrimStart('@'), parts[1].Trim()));
                    }
                    else
                    {
                        result.Add((item.Trim().TrimStart('@'), "Disabled"));
                    }
                }
            }
            return result;
        }

        private async Task SetupPuppeteerAsync()
        {
            try
            {
                // Configure BrowserFetcher to download Chromium into "ChromiumBrowser" folder
                var fetcherOptions = new BrowserFetcherOptions { Path = "ChromiumBrowser" };
                var fetcher = new BrowserFetcher(fetcherOptions);

                // Download Chromium: this will return a RevisionInfo object that has the executable path
                var revisionInfo = await fetcher.DownloadAsync();
                LaunchOptions launchOptions = new LaunchOptions
                {
                    Headless = true,
                    ExecutablePath = revisionInfo.GetExecutablePath(),
                    Args = new[] {"--no-sandbox", "--disable-setuid-sandbox" }
                };
                browser = await Puppeteer.LaunchAsync(launchOptions);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error setting up Puppeteer: " + ex.Message);
            }
        }

        private async Task FetchAllFeedsAsync(List<(string handle, string mode)> accounts, bool isBaseline)
        {
            if (browser != null)
            {
                foreach (var (handle, mode) in accounts)
                {
                    var fullHandle = "@" + handle;
                    if (mode.Equals("Disabled", StringComparison.OrdinalIgnoreCase)) continue;

                    IPage page = null;
                    try
                    {
                        var url = Settings.Default.BaseUrl.Replace("{account}", handle);

                        page = await browser.NewPageAsync();
                        page.DefaultNavigationTimeout = 1000;

                        // add UserAgent and some headers
                        await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/90.0.4430.212 Safari/537.36");
                        var headers = new Dictionary<string, string> { { "Accept", "application/json, text/plain, */*" } };
                        await page.SetExtraHttpHeadersAsync(headers);

                        // Read JSON from the file
                        var cookies = GetCookies(@"C:\Users\Sergey\Downloads\download.json");
                        await page.SetCookieAsync(cookies);

                        // Navigate and wait for network to be idle
                        await page.GoToAsync(url, WaitUntilNavigation.Networkidle0);

                        // Get HTML content of generated page
                        var content = await page.GetContentAsync();

                        List<TweetEntry> parsedTweets;
                        try
                        {
                            parsedTweets = ExtractTweetsFromPage(content);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error parsing tweets: {ex.Message}");
                            return;
                        }

                        var newTweetIds = new List<string>();

                        foreach (var tweet in parsedTweets)
                        {
                            var tweetId = tweet.EntryId;

                            if (!string.IsNullOrEmpty(tweetId))
                            {
                                if (!seenTweetIdsByUser[fullHandle].Contains(tweetId))
                                {
                                    seenTweetIdsByUser[fullHandle].Add(tweetId);
                                    if (!isBaseline)
                                    {
                                        newTweetIds.Add(tweetId);
                                        ShowNewTweetNotification(fullHandle, tweet.FullText, mode);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error fetching timeline for {fullHandle}: {ex.Message}");
                    }
                    finally
                    {
                        if (page != null)
                        {
                            try
                            {
                                await page.CloseAsync();
                                await page.DisposeAsync();
                                page = null;
                            }
                            catch { }
                        }
                    }
                }
            }
        }

        private void ShowNewTweetNotification(string account, string text, string mode)
        {
            Debug.WriteLine($"[NOTIFICATION] New tweet from {account}: {text}");

            if (mode.IndexOf("View", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                INotificationService notifyService = trayIcon;
                notifyService.Notify($"{account} posted new tweet", $"{text}");
            }

            if (mode.IndexOf("Sound", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                synthesizer.SpeakAsync($"{account} posted new tweet: {text}");
            }
        }

        private CookieParam[] GetCookies(string cookiesJsonFile)
        {
            var cookies = new List<CookieParam>();
            if (File.Exists(cookiesJsonFile))
            {
                string jsonContent = File.ReadAllText(cookiesJsonFile);

                // Deserialize the JSON into RawCookie objects
                var rawCookies = JsonConvert.DeserializeObject<List<RawCookie>>(jsonContent);

                if (rawCookies == null)
                {
                    throw new InvalidOperationException("Failed to parse cookies from JSON.");
                }

                // Map RawCookie to CookieParam
                foreach (var rc in rawCookies)
                {
                    cookies.Add(new CookieParam
                    {
                        Name = rc.Name,
                        Value = rc.Value,
                        Domain = rc.Domain,
                        Path = rc.Path,
                        Secure = rc.Secure,
                        HttpOnly = rc.HttpOnly,
                        SameSite = ParseSameSite(rc.SameSite),
                        Expires = rc.ExpirationDate
                    });
                }
            }

            return cookies.ToArray();
        }

        private SameSite? ParseSameSite(string sameSite)
        {
            if (string.IsNullOrEmpty(sameSite)) return null;
            sameSite = sameSite.ToLower();
            if (sameSite == "strict") return SameSite.Strict;
            if (sameSite == "lax") return SameSite.Lax;
            if (sameSite == "none" || sameSite == "no_restriction") return SameSite.None;
            return null;
        }

        private List<TweetEntry> ExtractTweetsFromPage(string pageContent)
        {
            var tweets = new List<TweetEntry>();

            // Locate and extract the JSON content
            var scriptId = "__NEXT_DATA__";
            var marker = $"<script id=\"{scriptId}\" type=\"application/json\">";
            var startIndex = pageContent.IndexOf(marker);

            string jsonContent = string.Empty;

            if (startIndex > 0)
            {
                startIndex += marker.Length;
                var endIndex = pageContent.IndexOf("</script>", startIndex);
                if (endIndex > 0)
                {
                    jsonContent = pageContent.Substring(startIndex, endIndex - startIndex);

                    try
                    {
                        // Parse the JSON content
                        using (var doc = JsonDocument.Parse(jsonContent))
                        {
                            var timeline = doc.RootElement
                                .GetProperty("props")
                                .GetProperty("pageProps")
                                .GetProperty("timeline")
                                .GetProperty("entries");

                            foreach (var entry in timeline.EnumerateArray())
                            {
                                if (entry.GetProperty("type").GetString() == "tweet")
                                {
                                    var tweetContent = entry.GetProperty("content").GetProperty("tweet");

                                    var tweet = new TweetEntry
                                    {
                                        EntryId = entry.GetProperty("entry_id").GetString(),
                                        CreatedAt = tweetContent.GetProperty("created_at").GetString(),
                                        FullText = tweetContent.GetProperty("full_text").GetString(),
                                        Permalink = tweetContent.GetProperty("permalink").GetString(),
                                        FavoriteCount = tweetContent.GetProperty("favorite_count").GetInt32(),
                                        RetweetCount = tweetContent.GetProperty("retweet_count").GetInt32(),
                                        ReplyCount = tweetContent.GetProperty("reply_count").GetInt32(),
                                        Lang = tweetContent.GetProperty("lang").GetString()
                                    };

                                    tweets.Add(tweet);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("Failed to parse tweets from page content.", ex);
                    }
                }
            }
            return tweets;
        }

        private void TrayIcon_ToggleVisibility(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (Visibility == Visibility.Hidden) Show(); else Hide();
            }
        }

        private async void TrayIcon_Exit(object sender, RoutedEventArgs e)
        {
            await ClosePuppeteerAsync();
            Application.Current.Shutdown();
        }

        private async Task ClosePuppeteerAsync()
        {
            try
            {
                if (browser != null)
                {
                    var pages = await browser.PagesAsync();
                    foreach (var page in pages)
                    {
                        try 
                        {
                            await page.CloseAsync();
                            await page.DisposeAsync();
                        } 
                        catch { }
                    }

                    await browser.CloseAsync();
                    await browser.DisposeAsync();
                    browser = null;
                }
            }
            catch { }
        }

    }

    public class TweetEntry
    {
        public string EntryId { get; set; }
        public string CreatedAt { get; set; }
        public string FullText { get; set; }
        public string Permalink { get; set; }
        public int FavoriteCount { get; set; }
        public int RetweetCount { get; set; }
        public int ReplyCount { get; set; }
        public string Lang { get; set; }
    }

    public class RawCookie
    {
        public string Domain { get; set; }
        public double? ExpirationDate { get; set; }
        public bool HttpOnly { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public string SameSite { get; set; }
        public bool Secure { get; set; }
        public string Value { get; set; }
    }

    public class AccountDisplay
    {
        public string TwitterHandle { get; set; }
        public string NotificationMode { get; set; }
    }
}
