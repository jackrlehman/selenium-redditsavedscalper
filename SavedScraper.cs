using System.Net.Http;
using OpenQA.Selenium;

internal sealed class SavedScraper
{
    private static readonly HttpClient HttpClient = new();

    private readonly IWebDriver driver;
    private readonly string username;
    private readonly bool unsaveAfterDownload;
    private readonly string downloadPath = Path.Combine(Environment.CurrentDirectory, RedditAppSettings.DownloadFolderName);
    private int iterator;
    private int downloadCount;

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

        await FindMediaAsync();
    }

    private async Task FindMediaAsync()
    {
        var done = false;
        var retryStartIterator = -1;

        while (!done)
        {
            var exceptionOccurred = false;
            IReadOnlyList<Task> currentDownloads = [];

            try
            {
                if (WaitHelper.WaitToCheckPresenceAndClick(driver, By.XPath(string.Format(RedditLocators.Saved.TableItemXpath, iterator + 1)), RedditAppSettings.LongWaitSeconds))
                {
                    iterator += 1;
                    currentDownloads = FindContentAndQueueDownloads();
                }
                else if (retryStartIterator >= 0 && retryStartIterator + RedditAppSettings.SavedTableRetryBuffer > iterator)
                {
                    Console.WriteLine("End of Saved table reached");
                    done = true;
                }
                else
                {
                    retryStartIterator = iterator;
                    Console.WriteLine($"End of table may have been reached at grid record #{iterator}.");
                    driver.Navigate().GoToUrl(RedditUrls.SavedPage(username));
                }
            }
            catch
            {
                exceptionOccurred = true;
                Console.WriteLine($"Unsupported media at grid record #{iterator}. Skipping record");
            }
            finally
            {
                await ExitFoundMediaAsync(exceptionOccurred, currentDownloads);
            }
        }
    }

    private IReadOnlyList<Task> FindContentAndQueueDownloads()
    {
        var contentFound = false;
        var possibleContentArray = false;
        var standardIndex = 0;
        var arrayIndex = 0;
        var currentDownloads = new List<Task>();

        while (!contentFound)
        {
            try
            {
                if (possibleContentArray)
                {
                    var source = GetArrayContentSource(arrayIndex);
                    QueueDownload(source, currentDownloads);
                    arrayIndex += 1;
                }
                else
                {
                    var source = GetStandardContentSource(standardIndex);
                    if (source.Contains("flair", StringComparison.OrdinalIgnoreCase))
                    {
                        standardIndex += 1;
                        if (standardIndex > 20)
                        {
                            throw new Exception("Element could not be located due to a most likely broken page");
                        }

                        continue;
                    }

                    QueueDownload(source, currentDownloads);
                    contentFound = true;
                }
            }
            catch (NoSuchElementException)
            {
                if (possibleContentArray && arrayIndex != 0)
                {
                    contentFound = true;
                }
                else if (!possibleContentArray)
                {
                    standardIndex += 1;
                    if (standardIndex > 10)
                    {
                        possibleContentArray = true;
                    }
                }
                else
                {
                    throw new Exception("Content not found");
                }
            }
        }

        return currentDownloads;
    }

    private string GetStandardContentSource(int offset)
    {
        var candidates = new List<string>
        {
            string.Format(RedditLocators.Saved.PostContentXpath, RedditAppSettings.PostContentStandardValue + offset)
        };

        candidates.AddRange(RedditLocators.Saved.StandardContentAlternateXpaths);
        return GetFirstHref(candidates);
    }

    private string GetArrayContentSource(int offset)
    {
        var candidates = RedditLocators.Saved.ArrayContentXpaths
            .Select(xpath => string.Format(xpath, RedditAppSettings.PostContentArrayStandardValue + offset));
        return GetFirstHref(candidates);
    }

    private string GetFirstHref(IEnumerable<string> candidates)
    {
        foreach (var xpath in candidates)
        {
            try
            {
                return driver.FindElement(By.XPath(xpath)).GetAttribute("href") ?? string.Empty;
            }
            catch (NoSuchElementException)
            {
            }
        }

        throw new NoSuchElementException();
    }

    private void QueueDownload(string source, ICollection<Task> currentDownloads)
    {
        if (source.Contains(".gif", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Unsupported Filetype: .gif");
            throw new Exception("Unsupported Filetype.");
        }

        var currentDownload = Interlocked.Increment(ref downloadCount);
        currentDownloads.Add(DownloadMediaAsync(source, currentDownload));
    }

    private async Task ExitFoundMediaAsync(bool exceptionOccurred, IReadOnlyCollection<Task> currentDownloads)
    {
        WaitHelper.WaitToCheckPresenceAndClick(driver, By.XPath(RedditLocators.Saved.PostContentCloseButtonXpath), RedditAppSettings.LongWaitSeconds);

        if (currentDownloads.Count > 0)
        {
            try
            {
                await Task.WhenAll(currentDownloads);
            }
            catch (Exception ex)
            {
                exceptionOccurred = true;
                Console.WriteLine($"Download failed at grid record #{iterator}. Post will remain saved. {ex.GetType().Name}: {ex.Message}");
            }
        }

        if (!unsaveAfterDownload)
        {
            return;
        }

        if (iterator != 1)
        {
            WaitHelper.CheckExistsAndClick(driver, By.XPath(RedditLocators.Saved.PostContentCloseButtonXpath));
            WaitHelper.CheckExistsAndClick(driver, By.XPath(RedditLocators.Saved.PostContentCloseButtonXpath));
            WaitHelper.CheckExistsAndClick(driver, By.XPath(RedditLocators.Saved.PostContentCloseButtonXpath));
            WaitHelper.CheckExistsAndClick(driver, By.XPath(RedditLocators.Saved.PostContentCloseButtonXpath));
        }

        if (exceptionOccurred)
        {
            return;
        }

        var resolved = false;
        var deletedUser1 = string.Format(RedditLocators.Saved.DeletedUser1Xpath, iterator);
        var deletedUser2 = string.Format(RedditLocators.Saved.DeletedUser2Xpath, iterator);

        if (WaitHelper.CheckExists(driver, By.XPath(deletedUser1)) &&
            driver.FindElement(By.XPath(deletedUser1)).Text.Contains("u/[deleted]", StringComparison.Ordinal))
        {
            WaitHelper.CheckExistsAndClick(driver, By.XPath(string.Format(RedditLocators.Saved.UnsaveButtonAlt3Xpath, iterator)));
            resolved = true;
        }
        else if (WaitHelper.CheckExists(driver, By.XPath(deletedUser2)) &&
                 driver.FindElement(By.XPath(deletedUser2)).Text.Contains("u/[deleted]", StringComparison.Ordinal))
        {
            WaitHelper.CheckExistsAndClick(driver, By.XPath(string.Format(RedditLocators.Saved.UnsaveButtonAlt2Xpath, iterator)));
            resolved = true;
        }

        if (!resolved)
        {
            WaitHelper.CheckExistsAndClick(driver, By.XPath(string.Format(RedditLocators.Saved.UnsaveButtonXpath, iterator)));
            WaitHelper.CheckExistsAndClick(driver, By.XPath(string.Format(RedditLocators.Saved.UnsaveButtonAlt1Xpath, iterator)));
            WaitHelper.CheckExistsAndClick(driver, By.XPath(string.Format(RedditLocators.Saved.UnsaveButtonAlt2Xpath, iterator)));
            WaitHelper.CheckExistsAndClick(driver, By.XPath(string.Format(RedditLocators.Saved.UnsaveButtonAlt3Xpath, iterator)));
        }

        Console.WriteLine($"Downloaded all media from grid record #{iterator}");
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

    private static string GetExtension(string source)
    {
        var uri = new Uri(source);
        var extension = Path.GetExtension(uri.AbsolutePath).TrimStart('.');
        return string.IsNullOrWhiteSpace(extension) ? "bin" : extension;
    }
}
