using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.ComponentModel;
using System.Configuration;
using System.Windows.Controls;
using System.Diagnostics;

using Newtonsoft.Json;
using NullSoftware.ToolKit;
using PuppeteerSharp;
using ModernWpf;
using HtmlAgilityPack;

using TweetNotify.Properties;

namespace TweetNotify
{
    public partial class MainWindow : Window
    {
        private IBrowser browser;
        private IPage page;

        private readonly DispatcherTimer timer = new DispatcherTimer();
        private List<(string handle, string mode)> accounts = new List<(string account, string mode)>();
        private readonly SpeechSynthesizer synthesizer = new SpeechSynthesizer();
        private Dictionary<string, (string Name, string Account, string Text)> seenTweet = 
            new Dictionary<string, (string Name, string Account, string Text)>();

        public MainWindow()
        {
            // For the firts run, locate window in the screen center, otherwise hide it
            if (Settings.Default.FirstRun) WindowStartupLocation = WindowStartupLocation.CenterScreen;
            else Visibility = Visibility.Hidden;

            InitializeComponent();
            InitializeAsync();
        }

        #region UI handling 

        /// <summary>
        /// Show/hime main window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TrayIcon_ToggleVisibility(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (Visibility == Visibility.Hidden) Show();
                else Hide();
            }
        }

        private void AboutMenu_Click(object sender, RoutedEventArgs e)
        {
            Topmost = false;
            var aboutBox = new AboutBox();
            aboutBox.ShowDialog();
            Topmost = true;
        }

        /// <summary>
        /// Shutdown the app
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void TrayIcon_Exit(object sender, RoutedEventArgs e)
        {
            timer?.Stop();
            await ClosePuppeteerAsync();
            KillHeadlessChromeInstances();
            Application.Current.Shutdown();
        }

        /// <summary>
        /// Kill all headless Chrome instances related to Puppeteer.
        /// </summary>
        private void KillHeadlessChromeInstances()
        {
            var puppeteerChromePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "ChromiumBrowser", "Chrome", "Win64-130.0.6723.69", "chrome-win64", "chrome.exe");

            try
            {
                var chromeProcesses = Process.GetProcessesByName("chrome");
                foreach (var process in chromeProcesses)
                {
                    try
                    {
                        // Match process path to Puppeteer Chrome path
                        if (process.MainModule != null && string.Equals(process.MainModule.FileName, puppeteerChromePath, StringComparison.OrdinalIgnoreCase))
                        {
                            process.Kill();
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void AccountModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.DataContext is AccountDisplay selectedAccount)
            {
                // Get the selected mode from the ComboBox
                string selectedMode = (comboBox.SelectedValue as string) ?? "Disabled";

                // Update the corresponding account's mode in the accounts list
                var accountIndex = accounts.FindIndex(a => $"@{a.handle}" == selectedAccount.TwitterHandle);
                if (accountIndex >= 0)
                {
                    accounts[accountIndex] = (accounts[accountIndex].handle, selectedMode);
                }

                // Save the updated accounts list to settings
                SaveAccountsToSettings();
            }
        }

        private async void AddAccount_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog("Enter X/Twitter Handle:", "Add new account");
            if (dialog.ShowDialog() == true)
            {
                var handle = dialog.Result;
                if (!string.IsNullOrWhiteSpace(handle))
                {
                    timer.Stop();

                    accounts.Add((handle, "View"));
                    var displayList = AccountsList.ItemsSource as List<AccountDisplay>;
                    displayList.Add(new AccountDisplay { TwitterHandle = $"@{handle}", NotificationMode = "Disabled" });
                    AccountsList.ItemsSource = null;
                    AccountsList.ItemsSource = displayList;
                    SaveAccountsToSettings();

                    // Refetch previous tweets
                    await ProcessNewTweetsAsync(accounts, true);
                    timer.Start();
                }
            }
        }

        private void DeleteAccount_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is AccountDisplay selectedAccount)
            {
                accounts.RemoveAll(a => $"@{a.handle}" == selectedAccount.TwitterHandle);

                var displayList = AccountsList.ItemsSource as List<AccountDisplay>;
                displayList.Remove(selectedAccount);
                AccountsList.ItemsSource = null;
                AccountsList.ItemsSource = displayList;
                SaveAccountsToSettings();
            }
        }

        private void SaveAccountsToSettings()
        {
            var serializedAccounts = string.Join(",", accounts.Select(a => $"{a.handle}:{a.mode}"));
            Settings.Default.TwitterAccounts = serializedAccounts;
            Settings.Default.Save();
        }

        private void BrowseCookieFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Cookie File",
                Filter = "JSON Files (*.json)|*.json",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
                Settings.Default.CookiesFileName = dialog.FileName;
        }

        private void ShowNewTweetNotification(string account, string text, string mode)
        {
            Debug.WriteLine($"[NOTIFICATION] New tweet from {account} with mode {mode}: {text}");
            account = account.Replace("@", "");

            if (mode.IndexOf("View", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                INotificationService notifyService = trayIcon;
                notifyService.Notify($"{account} posted new tweet", $"{text}");
            }

            if (mode.IndexOf("Sound", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                synthesizer.SpeakAsync($"{account} posted new tweet");
            }
        }

        #endregion

        #region Initialization

        private async void InitializeAsync()
        {
            // Hide main window and set attributes
            Topmost = true;
            if (Settings.Default.FirstRun) Settings.Default.FirstRun = false;
            else Hide();

            // Register settings changed handler
            Settings.Default.Save();
            Settings.Default.SettingChanging += AppSettingChanging;
            CookiesFileName.Text = Path.GetFileName(Settings.Default.CookiesFileName);

            // Populate voices into VoiceComboBox
            foreach (var voice in synthesizer.GetInstalledVoices())
                VoiceComboBox.Items.Add(voice.VoiceInfo.Name);
            VoiceComboBox.SelectedValue = Settings.Default.Voice;

            // Setup standard Windows speech synthesizer
            synthesizer.SetOutputToDefaultAudioDevice();
            synthesizer.SelectVoice(Settings.Default.Voice);
            synthesizer.Volume = Settings.Default.Volume;

            // Setup query timer
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += async (_, __) => { await ProcessNewTweetsAsync(accounts, false); };

            // Parse X/Twitter accounts
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
            }
            AccountsList.ItemsSource = displayList;

            // Hide window instead of closing
            Closing += (object sender, CancelEventArgs e) => { e.Cancel = true; Hide();};

            // Setup Puppeteer
            await SetupPuppeteerAsync();
        }

        private void AppSettingChanging(object sender, SettingChangingEventArgs e)
        {
            switch (e.SettingName)
            {
                case "UpdateTime":
                    timer.Interval = TimeSpan.FromSeconds((int)e.NewValue);
                    break;

                case "Voice":
                    synthesizer.SelectVoice((string)e.NewValue);
                    break;

                case "Volume":
                    synthesizer.Volume = (int)e.NewValue;
                    break;

                case "Theme":
                    ThemeManager.Current.ApplicationTheme = (string)e.NewValue == "Dark" ? ApplicationTheme.Dark : ApplicationTheme.Light;
                    break;

                case "CookiesFileName":
                    CookiesFileName.Text = Path.GetFileName((string)e.NewValue);
                    break;
            }
            Settings.Default.Save();
        }

        private List<(string handle, string mode)> ParseAccounts(string raw)
        {
            var result = new List<(string handle, string mode)>();
            if (!string.IsNullOrWhiteSpace(raw))
            {
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
                // Create headless Chromium browser
                var fetcherOptions = new BrowserFetcherOptions { Path = "ChromiumBrowser" };
                var fetcher = new BrowserFetcher(fetcherOptions);

                var revisionInfo = await fetcher.DownloadAsync();
                LaunchOptions launchOptions = new LaunchOptions
                {
                    Browser = SupportedBrowser.Chromium,
                    Headless = true,
                    //Headless = false,
                    ExecutablePath = revisionInfo.GetExecutablePath(),
                    Args = new[] { "--no-sandbox" }
                };
                browser = await Puppeteer.LaunchAsync(launchOptions);

                // Create browser page
                page = await browser.NewPageAsync();

                // Set UserAgent and headers
                string userAgent = @"Mozilla /5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/90.0.4430.212 Safari/537.36";
                await page.SetUserAgentAsync(userAgent);
                var headers = new Dictionary<string, string> { { "Accept", "application/json, text/plain, */*" } };
                await page.SetExtraHttpHeadersAsync(headers);

                // Set page cookies
                var cookies = GetCookies(Settings.Default.CookiesFileName);
                await page.SetCookieAsync(cookies);

                // Navigate to X Pro desks page
                var navigationTask = page.GoToAsync(Settings.Default.BaseUrl, new NavigationOptions
                {
                    WaitUntil = new[] { WaitUntilNavigation.Networkidle2 }
                });

                // Wait 10 seconds to load page
                var timeoutTask = Task.Delay(8000);
                var completedTask = await Task.WhenAny(navigationTask, timeoutTask);

                seenTweet = await ParseTweetsFromHTML();

                // Start update timer
                timer.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error setting up Puppeteer: " + ex.Message);
            }
        }
        #endregion

        #region Puppeteer fetching & processing
        
        private async Task<Dictionary<string, (string Name, string Account, string Text)>> ParseTweetsFromHTML()
        {
            var content = await page.GetContentAsync();

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(content);

            var selector = Settings.Default.Selector;

            // Try to detect new selector if changed
            var divs = htmlDoc.DocumentNode.SelectNodes("//div");
            if (divs != null)
            {
                var groupedAndSorted = divs
                    .Where(div => div.Attributes["class"] != null)
                    .Select(div => div.Attributes["class"].Value)
                    .GroupBy(className => className.Split(' ')[0])
                    .OrderByDescending(group => group.Count());

                var mostFrequentClass = groupedAndSorted.FirstOrDefault();
                if (mostFrequentClass != null) selector = mostFrequentClass.Key;
            }

            // Select all tweet containers based on identifiable structure
            var tweetContainers = htmlDoc.DocumentNode.SelectNodes($"//div[contains(@class, '{selector}')]");

            var tweets = new Dictionary<string, (string Name, string Handle, string Text)>();

            foreach (var container in tweetContainers)
            {
                // Extract user name
                var nameNode = container.SelectSingleNode(".//div[@data-testid='User-Name']//span");
                string name = nameNode?.InnerText ?? string.Empty;

                // Extract handle
                var handleNode = container.SelectSingleNode(".//div[@data-testid='User-Name']//span[contains(text(), '@')]");
                string handle = handleNode?.InnerText ?? string.Empty;

                // Extract tweet text
                var textNode = container.SelectSingleNode(".//div[@data-testid='tweetText']//span");
                string text = textNode?.InnerText ?? string.Empty;

                // Extract tweet URL
                var urlNode = container.SelectSingleNode(".//a[contains(@href, '/status/')]");
                string url = urlNode?.GetAttributeValue("href", string.Empty);

                // Add to the list of tweets
                if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(text) && !tweets.ContainsKey(url))
                {
                    tweets[url] = (name, handle, text);
                }
            }
            return tweets;
        }

        private async Task ProcessNewTweetsAsync(List<(string account, string mode)> accounts, bool isBaseline)
        {
            if (page != null)
            {
                // Parse the latest tweets
                var newTweets = await ParseTweetsFromHTML();

                // Find tweets that are in newTweets but not in seenTweet
                var trulyNewTweets = newTweets.Keys.Except(seenTweet.Keys)
                    .Select(key => (key, newTweets[key]))
                    .ToList();

                // If there are new tweets, process them
                if (trulyNewTweets.Count > 0)
                {
                    // Update seenTweet with all new tweets
                    foreach (var kvp in newTweets)
                        seenTweet[kvp.Key] = kvp.Value;

                    // Iterate through each account in `accounts`
                    foreach (var (account, mode) in accounts)
                    {
                        // Filter trulyNewTweets for tweets matching the current account
                        var matchingTweets = trulyNewTweets
                            .Where(ut => string.Equals(ut.Item2.Account, $"@{account}", StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        // Show notifications for matching tweets
                        foreach (var (_, (_, _, text)) in matchingTweets)
                        {
                            ShowNewTweetNotification(account, text, mode);
                        }
                    }
                }
            }
        }

        private CookieParam[] GetCookies(string cookiesJsonFile)
        {
            var cookies = new List<CookieParam>();
            if (File.Exists(cookiesJsonFile))
            {
                string jsonContent = File.ReadAllText(cookiesJsonFile);

                var rawCookies = JsonConvert.DeserializeObject<List<RawCookie>>(jsonContent) ?? throw new InvalidOperationException("Failed to parse cookies from JSON.");
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

        #endregion

    }

    #region Data classes
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
    #endregion
}
