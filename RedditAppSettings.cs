internal static class RedditAppSettings
{
    public const int ShortWaitSeconds = 5;
    public const int MediumWaitSeconds = 10;
    public const int LongWaitSeconds = 15;

    // Saved feed is lazy-loaded; scroll until this many passes reveal no new posts.
    public const int SavedFeedStableScrolls = 3;
    public const int SavedFeedScrollPauseMs = 1200;

    public const string DownloadFolderName = "Reddit Media";
}
