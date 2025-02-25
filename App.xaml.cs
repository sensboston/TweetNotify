﻿using System.Diagnostics;
using System;
using System.Threading;
using System.Windows;
using Bluegrams.Application;
using Microsoft.Toolkit.Uwp.Notifications;
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
            const string appName = "TweetNotify";
            bool createdNew;
            var mutex = new Mutex(true, appName, out createdNew);
            if (!createdNew)
            {
                // If an instance is already running, shut down this new one.
                Current.Shutdown();
                return;
            }

            // Register app for the toast notifications
            //ToastNotifierHelper.RegisterAppForNotifications();

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
