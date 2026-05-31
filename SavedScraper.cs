using OpenQA.Selenium;

internal sealed class SavedScraper
{
    private static readonly HttpClient HttpClient = new();

    private readonly IWebDriver driver;
    private readonly string username;
    private readonly bool unsaveAfterDownload;
    private readonly string downloadPath = Path.Combine(Environment.CurrentDirectory, RedditAppSettings.DownloadFolderName);
    private int downloadCount;
    private int postCount;

    public SavedScraper(IWebDriver driver, string username, bool unsaveAfterDownload)
    {
        this.driver = driver;
        this.username = username;
        this.unsaveAfterDownload = unsaveAfterDownload;
    }

    public async Task BeginAsync()
    {
        Console.WriteLine($"Download folder location: {downloadPath}");
        Directory.CreateDirectory(downloadPath);

        // When unsaving, each post must be handled while it is still on screen, since
        // the feed virtualizes off-screen posts out of the DOM. So download is awaited
        // per-post and the post is unsaved in place. When not unsaving there is no such
        // constraint, so downloads are started concurrently and awaited at the end.
        var deferredDownloads = new List<Task>();

        await ForEachSavedPostAsync(async (postElement, item) =>
        {
            if (unsaveAfterDownload)
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

        Console.WriteLine($"Done. Downloaded {downloadCount} files from {postCount} saved posts.");
    }

    // Read-only collection of every saved post (used by the diagnostic dry run).
    internal IReadOnlyList<SavedItem> CollectSavedItems()
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
        driver.Navigate().GoToUrl(RedditUrls.SavedPage(username));
        driver.Manage().Timeouts().AsynchronousJavaScript = TimeSpan.FromSeconds(RedditAppSettings.LongWaitSeconds);

        try
        {
            WaitHelper.WaitUntil(driver, RedditAppSettings.LongWaitSeconds, currentDriver =>
                currentDriver.FindElement(By.CssSelector(RedditLocators.Saved.PostCss)) != null);
        }
        catch (TimeoutException)
        {
            Console.WriteLine("No saved posts found (or the feed did not load).");
            return;
        }

        var processed = new HashSet<string>();
        var stableScrolls = 0;

        while (stableScrolls < RedditAppSettings.SavedFeedStableScrolls)
        {
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
            Console.WriteLine($"Skipping {item.PostId}: no downloadable media (post-type '{item.PostType}').");
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
            Console.WriteLine($"Download failed for {item.PostId}. Post will remain saved. {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private async Task DownloadMediaAsync(string source, int count)
    {
        var extension = GetExtension(source);
        var fileName = $"{DateTime.Now:MM-dd-yyyy HH-mm-ss-fff} {count}.{extension}";
        var filePath = Path.Combine(downloadPath, fileName);

        await using var sourceStream = await HttpClient.GetStreamAsync(source);
        await using var destinationStream = File.Create(filePath);
        await sourceStream.CopyToAsync(destinationStream);

        if (count % 10 == 0)
        {
            Console.WriteLine($"Total items downloaded: {count}");
        }
    }

    // Unsave by opening this post's overflow menu and clicking the save toggle.
    // Called while the post is still on screen (see BeginAsync). Selectors come from
    // the captured live menu; the click itself is intentionally never run during
    // development because it modifies the account.
    private void Unsave(SavedItem item)
    {
        var menuSelector = By.CssSelector($"{RedditLocators.Saved.OverflowMenuCss}[post-id='{item.PostId}']");
        if (!WaitHelper.CheckExists(driver, menuSelector))
        {
            Console.WriteLine($"Overflow menu for {item.PostId} not present; leaving it saved.");
            return;
        }

        var menu = driver.FindElement(menuSelector);
        ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", menu);
        Thread.Sleep(300);

        try
        {
            menu.FindElement(By.CssSelector(RedditLocators.Saved.OverflowTriggerCss)).Click();
        }
        catch (WebDriverException)
        {
            Console.WriteLine($"Could not open overflow menu for {item.PostId}; leaving it saved.");
            return;
        }

        if (!ClickFirstVisible(By.CssSelector(RedditLocators.Saved.UnsaveItemCss)))
        {
            Console.WriteLine($"Could not find the unsave item for {item.PostId}; leaving it saved.");
        }
    }

    // The overflow menu is portaled and pre-rendered (hidden) per post, so several
    // elements match the selector; click the one that is actually visible.
    private bool ClickFirstVisible(By selector)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(RedditAppSettings.ShortWaitSeconds);
        while (DateTime.UtcNow < deadline)
        {
            foreach (var element in driver.FindElements(selector))
            {
                try
                {
                    if (element.Displayed)
                    {
                        element.Click();
                        return true;
                    }
                }
                catch (WebDriverException)
                {
                }
            }

            Thread.Sleep(200);
        }

        return false;
    }

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
