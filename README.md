# Tweet Notify

## Desktop Notification Utility for X (formerly Twitter)

<img src="https://github.com/user-attachments/assets/8d4230d6-8344-431f-8084-1fada38c8441" width="300">
<img src="https://github.com/user-attachments/assets/d995d22d-e63d-4383-971c-a9acd01d59a4" width="300">

### Requirements
You must have a paid account on X to be able to create [X Pro Desk](https://pro.twitter.com/i/decks).

### Installation
This application is completely portable so it doesn't require installation. Just create a new folder (on the desktop or at any accessible folder on your profile), name it "TweetNotify" or whatever you like, download and move there the executable file **TweetNotify.exe**.

The application saves the settings to the **TweetNotify.config** file in the launch folder. The **ChromiumBrowser** folder (used for headless web scraping), will also be created there.
Checked setting **Start with Windows** will make a change in the registry at **SOFTWARE\Microsoft\Windows\CurrentVersion\Run**

### Setup
1. Create a new column or list on your **X Pro Desk** and add all the accounts from which you want to receive notifications.
2. Download and install the [Cookies Transfer Chrome extension](https://chromewebstore.google.com/detail/cookies-transfer/gglghmchcghfjeclmdjdhpigdcemleej?hl=en-US).
3. In Chrome, navigate to your [X Pro Desk](https://pro.twitter.com/i/decks) and log in if required.
4. Open the installed extension, provide the address `https://pro.twitter.com/i/decks`, and export the site cookies to a file named **download.json**.
5. Open the TweetNotify utility and go to **Settings** page:
   - Provide the location of the downloaded cookies file **download.json**.
   - In the **Base URL** field, copy and paste your actual X Pro Desk URL (it should look like `https://pro.twitter.com/i/decks/1679779121225836217`).
6. On **Accounts** page, add accounts for the periodical (every 1 second) check of the new posts/tweets. Please note, notification will be received for the listed accounts **only**, regardless how many accounts you're added to your **X Pro Desk** column.
