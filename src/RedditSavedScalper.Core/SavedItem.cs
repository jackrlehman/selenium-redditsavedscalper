namespace RedditSavedScalper.Core;

// One saved post and the media URLs to download for it. Extracted directly from
// the <shreddit-post> attributes / gallery JSON - no post needs to be opened.
public sealed record SavedItem(string PostId, string PostType, IReadOnlyList<string> MediaUrls);
