namespace RedditSavedScalper.Core;

// Streamed to the front-end as the scrape runs (a log line plus running counts).
public sealed record ScrapeProgress(string Message, int FilesDownloaded, int PostsProcessed);

// Returned when a scrape finishes.
public sealed record ScrapeResult(int PostsFound, int FilesDownloaded, int Skipped);
