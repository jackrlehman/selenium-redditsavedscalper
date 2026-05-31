using Microsoft.Maui.Storage;

namespace RedditSavedScalper.Maui.Services;

// Front-loaded options, persisted between runs via MAUI Preferences.
public static class AppSettings
{
    public static bool UnsaveAfterDownload
    {
        get => Preferences.Get(nameof(UnsaveAfterDownload), false);
        set => Preferences.Set(nameof(UnsaveAfterDownload), value);
    }

    public static string DownloadFolder
    {
        get => Preferences.Get(nameof(DownloadFolder), DefaultDownloadFolder);
        set => Preferences.Set(nameof(DownloadFolder), value);
    }

    public static bool OpenFolderWhenDone
    {
        get => Preferences.Get(nameof(OpenFolderWhenDone), true);
        set => Preferences.Set(nameof(OpenFolderWhenDone), value);
    }

    // Run the scrape with no visible Chrome window (login always stays visible).
    // On by default - most runs don't need to watch the browser.
    public static bool Headless
    {
        get => Preferences.Get(nameof(Headless), true);
        set => Preferences.Set(nameof(Headless), value);
    }

    // Optimistic indicator of who last seeded a session (actual validity is checked
    // against the live browser when a scrape starts).
    public static string LastLoggedInUser
    {
        get => Preferences.Get(nameof(LastLoggedInUser), string.Empty);
        set => Preferences.Set(nameof(LastLoggedInUser), value);
    }

    public static string DefaultDownloadFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Reddit Media");

    // Persistent Chrome profile that holds the logged-in session, kept in app data.
    public static string ProfileDirectory =>
        Path.Combine(FileSystem.AppDataDirectory, "chrome-profile");
}
