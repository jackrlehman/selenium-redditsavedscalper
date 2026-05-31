// One saved post and the media URLs to download for it. Extracted directly from
// the <shreddit-post> attributes / gallery carousel - no post needs to be opened.
internal sealed record SavedItem(string PostId, string PostType, IReadOnlyList<string> MediaUrls);
