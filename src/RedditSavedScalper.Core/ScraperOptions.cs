namespace RedditSavedScalper.Core;

// Everything a scrape run needs, supplied by whichever front-end (console or GUI).
public sealed class ScraperOptions
{
    public required string Username { get; init; }

    public bool UnsaveAfterDownload { get; init; }

    // Where media is saved. Null/empty => a "Reddit Media" folder in the base directory.
    public string? DownloadFolder { get; init; }

    // Persistent Chrome profile directory (holds the logged-in session). Null => a
    // fresh, throwaway browser session each run.
    public string? ProfileDirectory { get; init; }

    // Run Chrome with no visible window. Only applied to the scrape browser; the
    // manual-login browser is always visible so the user can sign in.
    public bool Headless { get; init; }

    public string ResolveDownloadFolder() =>
        string.IsNullOrWhiteSpace(DownloadFolder)
            ? Path.Combine(Environment.CurrentDirectory, RedditAppSettings.DownloadFolderName)
            : DownloadFolder;
}
