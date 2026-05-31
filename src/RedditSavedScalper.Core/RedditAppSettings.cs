namespace RedditSavedScalper.Core;

public static class RedditAppSettings
{
    public const int ShortWaitSeconds = 5;
    public const int MediumWaitSeconds = 10;
    public const int LongWaitSeconds = 15;

    // Saved feed is lazy-loaded; scroll until this many passes reveal no new posts.
    public const int SavedFeedStableScrolls = 3;
    public const int SavedFeedScrollPauseMs = 1200;

    // Media-heavy Reddit pages can keep loading for a long time; bound navigation so
    // a stuck page surfaces an error instead of hanging the app.
    public const int PageLoadTimeoutSeconds = 60;

    public const string DownloadFolderName = "Reddit Media";
}
