using OpenQA.Selenium;

namespace RedditSavedScalper.Core;

public sealed class SavedScraper
{
    private static readonly HttpClient HttpClient = new();

    private readonly IWebDriver driver;
    private readonly ScraperOptions options;
    private readonly IProgress<ScrapeProgress>? progress;
    private readonly CancellationToken cancellationToken;
    private readonly string downloadPath;
    private int downloadCount;
    private int postCount;
    private int skipCount;

    public SavedScraper(
        IWebDriver driver,
        ScraperOptions options,
        IProgress<ScrapeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        this.driver = driver;
        this.options = options;
        this.progress = progress;
        this.cancellationToken = cancellationToken;
        downloadPath = options.ResolveDownloadFolder();
    }

    public async Task<ScrapeResult> RunAsync()
    {
        Report($"Download folder: {downloadPath}");

        // Create the folder (and any missing parents) up front. If the path is
        // unusable (bad drive, illegal characters, no permission) fail clearly here
        // rather than mid-download with a cryptic per-file error.
        try
        {
            Directory.CreateDirectory(downloadPath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Could not use the download folder '{downloadPath}': {ex.Message}", ex);
        }

        // When unsaving, each post must be handled while it is still on screen, since
        // the feed virtualizes off-screen posts out of the DOM. So download is awaited
        // per-post and the post is unsaved in place. When not unsaving there is no such
        // constraint, so downloads are started concurrently and awaited at the end.
        var deferredDownloads = new List<Task>();

        await ForEachSavedPostAsync(async (postElement, item) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (options.UnsaveAfterDownload)
            {
                if (await DownloadItemAsync(item))
                {
                    Unsave(item);
                }
            }
            else
            {
                deferredDownloads.Add(DownloadItemAsync(item));
            }
        });

        await Task.WhenAll(deferredDownloads);

        Report($"Done. Downloaded {downloadCount} files from {postCount} saved posts.");
        return new ScrapeResult(postCount, downloadCount, skipCount);
    }

    // Read-only collection of every saved post (used by the diagnostic dry run).
    public IReadOnlyList<SavedItem> CollectSavedItems()
    {
        var items = new List<SavedItem>();
        ForEachSavedPostAsync((_, item) =>
        {
            items.Add(item);
            return Task.CompletedTask;
        }).GetAwaiter().GetResult();
        return items;
    }

    // Scrolls the lazy-loaded saved feed to the end, invoking handler once per post
    // while that post is still rendered. Posts are keyed by id so virtualization /
    // re-rendering never double-counts. Stops once several scrolls reveal nothing new.
    private async Task ForEachSavedPostAsync(Func<IWebElement, SavedItem, Task> handler)
    {
        driver.Navigate().GoToUrl(RedditUrls.SavedPage(options.Username));
        driver.Manage().Timeouts().AsynchronousJavaScript = TimeSpan.FromSeconds(RedditAppSettings.LongWaitSeconds);

        try
        {
            WaitHelper.WaitUntil(driver, RedditAppSettings.LongWaitSeconds, currentDriver =>
                currentDriver.FindElement(By.CssSelector(RedditLocators.Saved.PostCss)) != null);
        }
        catch (TimeoutException)
        {
            Report("No saved posts found (or the feed did not load).");
            return;
        }

        var processed = new HashSet<string>();
        var stableScrolls = 0;

        while (stableScrolls < RedditAppSettings.SavedFeedStableScrolls)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var countBefore = processed.Count;

            foreach (var postElement in driver.FindElements(By.CssSelector(RedditLocators.Saved.PostCss)))
            {
                var item = ExtractItem(postElement);
                if (item == null || !processed.Add(item.PostId))
                {
                    continue;
                }

                postCount += 1;
                await handler(postElement, item);
            }

            ((IJavaScriptExecutor)driver).ExecuteScript("window.scrollBy(0, window.innerHeight * 2);");
            Thread.Sleep(RedditAppSettings.SavedFeedScrollPauseMs);

            stableScrolls = processed.Count == countBefore ? stableScrolls + 1 : 0;
        }
    }

    private SavedItem? ExtractItem(IWebElement postElement)
    {
        try
        {
            var id = postElement.GetAttribute(RedditLocators.Saved.IdAttribute) ?? string.Empty;
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            var type = postElement.GetAttribute(RedditLocators.Saved.PostTypeAttribute) ?? string.Empty;
            var urls = new List<string>();

            if (type == RedditLocators.Saved.PostType_Gallery)
            {
                urls.AddRange(GetGalleryUrls(postElement));
            }
            else
            {
                var href = postElement.GetAttribute(RedditLocators.Saved.ContentHrefAttribute) ?? string.Empty;
                if (IsDirectMedia(href))
                {
                    urls.Add(href);
                }
            }

            return new SavedItem(id, type, urls);
        }
        catch (StaleElementReferenceException)
        {
            return null;
        }
    }

    // Galleries: every image at full resolution comes from the post JSON. The
    // lazy-loaded carousel thumbnails are only a fallback if the JSON is unavailable.
    private List<string> GetGalleryUrls(IWebElement postElement)
    {
        var permalink = postElement.GetAttribute(RedditLocators.Saved.PermalinkAttribute) ?? string.Empty;
        if (!string.IsNullOrEmpty(permalink))
        {
            var fromJson = FetchGalleryMediaUrls(permalink);
            if (fromJson.Count > 0)
            {
                return fromJson;
            }
        }

        var fallback = new List<string>();
        foreach (var image in postElement.FindElements(By.CssSelector(RedditLocators.Saved.GalleryImageCss)))
        {
            var src = image.GetAttribute("src");
            if (!string.IsNullOrEmpty(src))
            {
                fallback.Add(src);
            }
        }

        return fallback;
    }

    // Fetches the post JSON in the browser (so the session cookies apply, letting
    // NSFW galleries resolve) and builds full-resolution i.redd.it URLs in order.
    private List<string> FetchGalleryMediaUrls(string permalink)
    {
        const string script = @"
            const cb = arguments[arguments.length - 1];
            fetch(arguments[0] + '.json', {credentials:'include'})
              .then(r => r.json())
              .then(d => {
                  const p = d[0].data.children[0].data;
                  const mm = p.media_metadata || {};
                  const order = (p.gallery_data && p.gallery_data.items) || [];
                  const urls = order.map(it => {
                      const m = mm[it.media_id]; if (!m) return null;
                      const ext = (m.m || '').split('/')[1] || 'jpg';
                      return 'https://i.redd.it/' + it.media_id + '.' + ext;
                  }).filter(Boolean);
                  cb(urls.join('\n'));
              })
              .catch(() => cb(''));";

        try
        {
            var result = ((IJavaScriptExecutor)driver).ExecuteAsyncScript(script, permalink) as string ?? string.Empty;
            return result.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
        }
        catch (WebDriverException)
        {
            return [];
        }
    }

    // Downloads every media URL for a post. Returns false (so the post is left saved)
    // when there is nothing to download or a download fails.
    private async Task<bool> DownloadItemAsync(SavedItem item)
    {
        if (item.MediaUrls.Count == 0)
        {
            skipCount += 1;
            Report($"Skipping {item.PostId}: no downloadable media (post-type '{item.PostType}').");
            return false;
        }

        try
        {
            foreach (var url in item.MediaUrls)
            {
                await DownloadMediaAsync(url, Interlocked.Increment(ref downloadCount));
            }

            return true;
        }
        catch (Exception ex)
        {
            // Only mention staying saved when unsaving was actually on the table.
            var savedNote = options.UnsaveAfterDownload ? " It will stay saved." : string.Empty;
            Report($"Download failed for {item.PostId}.{savedNote} {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private async Task DownloadMediaAsync(string source, int count)
    {
        var extension = GetExtension(source);
        var fileName = $"{DateTime.Now:MM-dd-yyyy HH-mm-ss-fff} {count}.{extension}";
        var filePath = Path.Combine(downloadPath, fileName);

        await using var sourceStream = await HttpClient.GetStreamAsync(source, cancellationToken);
        await using var destinationStream = File.Create(filePath);
        await sourceStream.CopyToAsync(destinationStream, cancellationToken);

        if (count % 10 == 0)
        {
            Report($"Total items downloaded: {count}");
        }
    }

    // Unsave by opening this post's overflow menu and clicking the save toggle.
    // Called while the post is still on screen (see RunAsync). Selectors come from
    // the captured live menu; the click itself was never run during development
    // because it modifies the account.
    private void Unsave(SavedItem item)
    {
        var menuSelector = By.CssSelector($"{RedditLocators.Saved.OverflowMenuCss}[post-id='{item.PostId}']");
        if (!WaitHelper.CheckExists(driver, menuSelector))
        {
            Report($"Overflow menu for {item.PostId} not present; leaving it saved.");
            return;
        }

        // Bring it into view so the menu popover lays out correctly, then do the whole
        // open-and-click in the browser (see UnsaveScript).
        ((IJavaScriptExecutor)driver).ExecuteScript(
            "arguments[0].scrollIntoView({block:'center'});",
            driver.FindElement(menuSelector));
        Thread.Sleep(200);

        string outcome;
        try
        {
            outcome = ((IJavaScriptExecutor)driver).ExecuteAsyncScript(UnsaveScript, item.PostId) as string ?? "error";
        }
        catch (WebDriverException ex)
        {
            outcome = ex.GetType().Name;
        }

        if (outcome != "ok" && outcome != "already-unsaved")
        {
            Report($"Could not unsave {item.PostId} ({outcome}); leaving it saved.");
        }
    }

    // Opens the post's overflow menu and clicks the (visible) "Remove from saved"
    // toggle, entirely in the browser. The menu items are portaled and can live in
    // shadow DOM, so we walk shadow roots and poll for the menu to render, then click
    // via JS (which avoids Selenium's strict popover visibility/obscured checks). The
    // is-post-saved guard means an already-unsaved post is never re-saved by mistake.
    private const string UnsaveScript = @"
        const postId = arguments[0];
        const cb = arguments[arguments.length - 1];

        function* walk(root) {
            for (const el of root.querySelectorAll('*')) {
                yield el;
                if (el.shadowRoot) yield* walk(el.shadowRoot);
            }
        }
        function visible(el) {
            if (!el) return false;
            const r = el.getBoundingClientRect();
            return r.width > 0 && r.height > 0;
        }

        const menuEl = document.querySelector(`shreddit-post-overflow-menu[post-id='${postId}']`);
        if (!menuEl) { cb('no-menu'); }
        else if (!menuEl.hasAttribute('is-post-saved')) { cb('already-unsaved'); }
        else {
            const trigger = menuEl.querySelector(""button[aria-label='Open user actions']"");
            if (!trigger) { cb('no-trigger'); }
            else {
                if (trigger.getAttribute('aria-expanded') !== 'true') trigger.click();
                let tries = 0;
                const timer = setInterval(() => {
                    tries++;
                    let target = null;
                    for (const el of walk(document)) {
                        if (el.id === 'post-overflow-save') {
                            const mi = el.getAttribute('role') === 'menuitem' ? el : el.querySelector(""[role='menuitem']"");
                            if (visible(mi)) { target = mi; break; }
                        }
                    }
                    if (target) { clearInterval(timer); target.click(); cb('ok'); }
                    else if (tries > 40) { clearInterval(timer); cb('item-not-found'); }
                }, 150);
            }
        }";

    private void Report(string message) =>
        progress?.Report(new ScrapeProgress(message, downloadCount, postCount));

    private static bool IsDirectMedia(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        var path = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.AbsolutePath : url;
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".mp4";
    }

    private static string GetExtension(string source)
    {
        var uri = new Uri(source);
        var extension = Path.GetExtension(uri.AbsolutePath).TrimStart('.');
        return string.IsNullOrWhiteSpace(extension) ? "bin" : extension;
    }
}
