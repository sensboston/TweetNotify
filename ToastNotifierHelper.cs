﻿using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using Microsoft.Toolkit.Uwp.Notifications;

namespace TweetNotify
{
    internal class ToastNotifierHelper
    {
        public static void ShowNotification(string title, string text, string permLink)
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(text)
                .AddAppLogoOverride(new Uri(GetLogoPath()))
                .AddButton(new ToastButton()
                    .SetContent("Open tweet")
                    .SetProtocolActivation(new Uri(permLink))
            ).Show();
        }

        private static string GetLogoPath()
        {
            var logoFilePath = Path.Combine(Path.GetTempPath(), "App.png");
            if (!File.Exists(logoFilePath))
            {
                using (Stream resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TweetNotify.App.png"))
                    if (resourceStream != null)
                        using (Bitmap bitmap = new Bitmap(resourceStream))
                            bitmap.Save(logoFilePath, ImageFormat.Png);
            }
            return logoFilePath;
        }
    }
}

