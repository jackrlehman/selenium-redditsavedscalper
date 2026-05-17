using System.Buffers;
using System.Net.Http;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

internal static class Program
{
    private const int ShortWaitSeconds = 5;
    private const int MediumWaitSeconds = 10;
    private const int LongWaitSeconds = 15;

    private static async Task Main()
    {
        Console.WriteLine("Welcome to Reddit Saved Scalper by jackrlehman. Converted to C# Selenium. The scalper will download all images from your reddit saved page. Please ensure you have Google Chrome installed on this device and do not interfere with the newly opened Chrome application.");

        try
        {
            var options = new ChromeOptions();
            options.AddArgument("--disable-infobars");
            options.AddArgument("start-maximized");
            options.AddArgument("--disable-extensions");
            options.AddExcludedArgument("enable-logging");
            options.AddUserProfilePreference("profile.default_content_setting_values.notifications", 1);

            using var driver = new ChromeDriver(options);

            try
            {
                var username = Prompt("Enter Reddit Username: ");
                var password = PromptPassword("Enter Reddit Password: ");

                try
                {
                    var unsavePostAfterDownload = PromptForUnsavePreference();

                    Console.WriteLine("Process started. You will find a folder called 'Reddit Media' containing the downloaded contents in your base directory.");

                    driver.Navigate().GoToUrl("https://www.reddit.com/");
                    var login = new Login(driver, username, password);
                    login.PerformLogin();

                    var saved = new SavedScraper(driver, username, unsavePostAfterDownload);
                    await saved.BeginAsync();
                }
                finally
                {
                    Array.Clear(password, 0, password.Length);
                }

                Console.WriteLine("All items downloaded");
            }
            catch
            {
                Console.WriteLine("An unresolvable issue occurred. Application shutting down.");
            }
        }
        catch
        {
            Console.WriteLine("There was an issue regarding Google Chrome. Please ensure you have Google Chrome installed on this device.");
        }
    }

    private static string Prompt(string message) => ConsoleHelper.ReadLineWithPrompt(message);

    private static char[] PromptPassword(string message) => ConsoleHelper.ReadPasswordWithPrompt(message);

    private static bool PromptForUnsavePreference()
    {
        while (true)
        {
            var response = Prompt("Would you like to unsave the post after download? (y/n): ").Trim().ToLowerInvariant();

            if (response == "y")
            {
                return true;
            }

            if (response == "n")
            {
                return false;
            }

            Console.WriteLine($"{response} is not a valid input.");
        }
    }

    private sealed class Login
    {
        private const string LoginPopupButtonXpath = "/html/body/div[1]/div/div[2]/div[1]/header/div/div[2]/div/div[1]/a[1]";
        private const string LoginFormIframeXpath = "/html/body/div[1]/div/div[2]/div[3]/div/div/iframe";
        private const string UsernameFieldId = "loginUsername";
        private const string PasswordFieldId = "loginPassword";
        private const string LoginFormButtonXpath = "/html/body/div/main/div[1]/div/div/form/fieldset[4]/button";
        private const string LoginConfirmationXpath = "/html/body/div[1]/div/div[2]/div[3]/div/div/div[1]";
        private const string ExpectedLoginConfirmation = "logged in";
        private const string InterestsPopupCloseButtonXpath = "/html/body/div[1]/div/div[2]/div[4]/div/div/div/header/div/div[2]/button/i";

        private readonly IWebDriver driver;
        private readonly string username;
        private readonly char[] password;

        public Login(IWebDriver driver, string username, char[] password)
        {
            this.driver = driver;
            this.username = username;
            this.password = password;
        }

        public void PerformLogin()
        {
            try
            {
                WaitHelper.WaitToBeClickableAndClick(driver, By.XPath(LoginPopupButtonXpath), ShortWaitSeconds);
                WaitHelper.WaitUntil(driver, ShortWaitSeconds, currentDriver =>
                {
                    currentDriver.SwitchTo().DefaultContent();
                    var frame = currentDriver.FindElement(By.XPath(LoginFormIframeXpath));
                    currentDriver.SwitchTo().Frame(frame);
                    return true;
                });

                WaitHelper.WaitToBeClickableAndSendKeys(driver, By.Id(UsernameFieldId), username, ShortWaitSeconds);
                WaitHelper.WaitToBeClickableAndSendKeys(driver, By.Id(PasswordFieldId), new string(password), ShortWaitSeconds);

                WaitHelper.WaitToBeClickableAndClick(driver, By.XPath(LoginFormButtonXpath), ShortWaitSeconds);
                WaitHelper.WaitUntil(driver, MediumWaitSeconds, currentDriver =>
                    currentDriver.FindElement(By.XPath(LoginConfirmationXpath)).Text.Contains(ExpectedLoginConfirmation, StringComparison.OrdinalIgnoreCase));

                driver.SwitchTo().DefaultContent();
                WaitHelper.WaitToCheckPresenceAndClick(driver, By.XPath(InterestsPopupCloseButtonXpath), MediumWaitSeconds);
                driver.Navigate().GoToUrl($"https://www.reddit.com/user/{username}/saved/");
                Console.WriteLine("Login Successful");
            }
            catch
            {
                Console.WriteLine("Login Failed");
                throw new Exception("Login Failed");
            }
        }
    }

    private sealed class SavedScraper
    {
        private const string TableItemXpath = "/html/body/div[1]/div/div[2]/div[2]/div/div/div/div[2]/div[3]/div[1]/div[2]/div[1]/div[{0}]";
        private const string PostContentXpath = "/html/body/div[1]/div/div[2]/div[3]/div/div/div/div[2]/div[1]/div[2]/div[1]/div/div[{0}]/div/a";
        private const string PostContentAlt1Xpath = "/html/body/div[1]/div/div[2]/div[3]/div/div/div/div[2]/div[1]/div[2]/div[1]/div/div[5]/div[3]/div[1]/div/a";
        private const string PostContentAlt2Xpath = "/html/body/div[1]/div/div[2]/div[2]/div/div/div/div[2]/div[3]/div[1]/div[2]/div[1]/div/div[5]/div[3]/div[1]/div/a";
        private const string PostContentAlt3Xpath = "/html/body/div[1]/div/div[2]/div[3]/div/div/div/div[2]/div[1]/div[2]/div[1]/div/div[5]/div/a";
        private const string PostContentAlt4Xpath = "/html/body/div[1]/div/div[2]/div[3]/div/div/div/div[2]/div[1]/div[3]/div[1]/div/div[5]/div/a";
        private const string PostContentAlt5Xpath = "/html/body/div[1]/div/div[2]/div[3]/div/div/div/div[2]/div[1]/div[3]/div[1]/div/div[5]/div[3]/div[1]/div/a";
        private const string PostContentAlt6Xpath = "/html/body/div[1]/div/div[2]/div[3]/div/div/div/div[2]/div[1]/div[3]/div[1]/div/div[6]/div/a";
        private const int PostContentStandardValue = 5;
        private const string PostContentArrayXpath = "/html/body/div[1]/div/div[2]/div[3]/div/div/div/div[2]/div[1]/div[2]/div[1]/div/div/div/div[5]/div[1]/div/div[1]/ul/li[{0}]/figure/a";
        private const string PostContentArrayAlt1Xpath = "/html/body/div[1]/div/div[2]/div[3]/div/div/div/div[2]/div[1]/div[2]/div[1]/div/div[5]/div[3]/div/div[1]/div/div[1]/ul/li[{0}]/figure/a";
        private const string PostContentArrayAlt2Xpath = "/html/body/div[1]/div/div[2]/div[3]/div/div/div/div[2]/div[1]/div[3]/div[1]/div/div/div/div[5]/div[1]/div/div[1]/ul/li[{0}]/figure/a";
        private const int PostContentArrayStandardValue = 1;
        private const string DeletedUser1Xpath = "/html/body/div[1]/div/div[2]/div[2]/div/div/div/div[2]/div[3]/div[1]/div[2]/div[1]/div[{0}]/div/div/div[2]/div/div[2]/div[2]/div[2]/span[2]";
        private const string DeletedUser2Xpath = "/html/body/div[1]/div/div[2]/div[2]/div/div/div/div[2]/div[3]/div[1]/div[2]/div[1]/div[{0}]/div/div/div/div/div[2]/div/div[2]/div[2]/div[2]/span[2]";
        private const string UnsaveButtonXpath = "/html/body/div[1]/div/div[2]/div[2]/div/div/div/div[2]/div[3]/div[1]/div[2]/div[1]/div[{0}]/div/div/div[2]/div/div[2]/div[3]/div[3]/div[3]/button";
        private const string UnsaveButtonAlt1Xpath = "/html/body/div[1]/div/div[2]/div[2]/div/div/div/div[2]/div[3]/div[1]/div[2]/div[1]/div[{0}]/div/div/div/div/div[2]/div/div[2]/div[3]/div[3]/div[3]/button";
        private const string UnsaveButtonAlt2Xpath = "/html/body/div[1]/div/div[2]/div[2]/div/div/div/div[2]/div[3]/div[1]/div[2]/div[1]/div[{0}]/div/div/div/div/div[2]/div/div[2]/div[3]/div[3]/div[2]/button";
        private const string UnsaveButtonAlt3Xpath = "/html/body/div[1]/div/div[2]/div[2]/div/div/div/div[2]/div[3]/div[1]/div[2]/div[1]/div[{0}]/div/div/div[2]/div/div[2]/div[3]/div[3]/div[2]/button";
        private const string PostContentCloseButtonXpath = "/html/body/div[1]/div/div[2]/div[3]/div/div/div/div[1]/div/div[2]/button";
        private const int SavedTableRetryBuffer = 3;

        private static readonly string[] StandardContentAlternateXpaths =
        [
            PostContentAlt1Xpath,
            PostContentAlt2Xpath,
            PostContentAlt3Xpath,
            PostContentAlt4Xpath,
            PostContentAlt5Xpath,
            PostContentAlt6Xpath
        ];

        private static readonly string[] ArrayContentXpaths =
        [
            PostContentArrayXpath,
            PostContentArrayAlt1Xpath,
            PostContentArrayAlt2Xpath
        ];

        private readonly IWebDriver driver;
        private readonly string username;
        private readonly bool unsaveAfterDownload;
        private static readonly HttpClient HttpClient = new();
        private readonly string downloadPath = Path.Combine(Environment.CurrentDirectory, "Reddit Media");
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
                    if (WaitHelper.WaitToCheckPresenceAndClick(driver, By.XPath(string.Format(TableItemXpath, iterator + 1)), LongWaitSeconds))
                    {
                        iterator += 1;
                        currentDownloads = FindContentAndQueueDownloads();
                    }
                    else if (retryStartIterator >= 0 && retryStartIterator + SavedTableRetryBuffer > iterator)
                    {
                        Console.WriteLine("End of Saved table reached");
                        done = true;
                    }
                    else
                    {
                        retryStartIterator = iterator;
                        Console.WriteLine($"End of table may have been reached at grid record #{iterator}.");
                        driver.Navigate().GoToUrl($"https://www.reddit.com/user/{username}/saved/");
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
            var candidates = new List<string> { string.Format(PostContentXpath, PostContentStandardValue + offset) };
            candidates.AddRange(StandardContentAlternateXpaths);
            return GetFirstHref(candidates);
        }

        private string GetArrayContentSource(int offset)
        {
            var candidates = ArrayContentXpaths.Select(xpath => string.Format(xpath, PostContentArrayStandardValue + offset));
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
            WaitHelper.WaitToCheckPresenceAndClick(driver, By.XPath(PostContentCloseButtonXpath), LongWaitSeconds);

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
                WaitHelper.CheckExistsAndClick(driver, By.XPath(PostContentCloseButtonXpath));
                WaitHelper.CheckExistsAndClick(driver, By.XPath(PostContentCloseButtonXpath));
                WaitHelper.CheckExistsAndClick(driver, By.XPath(PostContentCloseButtonXpath));
                WaitHelper.CheckExistsAndClick(driver, By.XPath(PostContentCloseButtonXpath));
            }

            if (exceptionOccurred)
            {
                return;
            }

            var resolved = false;
            var deletedUser1 = string.Format(DeletedUser1Xpath, iterator);
            var deletedUser2 = string.Format(DeletedUser2Xpath, iterator);

            if (WaitHelper.CheckExists(driver, By.XPath(deletedUser1)) &&
                driver.FindElement(By.XPath(deletedUser1)).Text.Contains("u/[deleted]", StringComparison.Ordinal))
            {
                WaitHelper.CheckExistsAndClick(driver, By.XPath(string.Format(UnsaveButtonAlt3Xpath, iterator)));
                resolved = true;
            }
            else if (WaitHelper.CheckExists(driver, By.XPath(deletedUser2)) &&
                     driver.FindElement(By.XPath(deletedUser2)).Text.Contains("u/[deleted]", StringComparison.Ordinal))
            {
                WaitHelper.CheckExistsAndClick(driver, By.XPath(string.Format(UnsaveButtonAlt2Xpath, iterator)));
                resolved = true;
            }

            if (!resolved)
            {
                WaitHelper.CheckExistsAndClick(driver, By.XPath(string.Format(UnsaveButtonXpath, iterator)));
                WaitHelper.CheckExistsAndClick(driver, By.XPath(string.Format(UnsaveButtonAlt1Xpath, iterator)));
                WaitHelper.CheckExistsAndClick(driver, By.XPath(string.Format(UnsaveButtonAlt2Xpath, iterator)));
                WaitHelper.CheckExistsAndClick(driver, By.XPath(string.Format(UnsaveButtonAlt3Xpath, iterator)));
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

    private static class WaitHelper
    {
        public static IWebElement WaitForClickable(IWebDriver driver, By selector, int waitSeconds)
        {
            IWebElement? element = null;
            WaitUntil(driver, waitSeconds, currentDriver =>
            {
                var candidate = currentDriver.FindElement(selector);
                if (!candidate.Displayed || !candidate.Enabled)
                {
                    return false;
                }

                element = candidate;
                return true;
            });

            return element ?? throw new TimeoutException("The Selenium element was not clickable.");
        }

        public static void WaitToBeClickableAndClick(IWebDriver driver, By selector, int waitSeconds)
        {
            WaitForClickable(driver, selector, waitSeconds).Click();
        }

        public static void WaitToBeClickableAndSendKeys(IWebDriver driver, By selector, string keys, int waitSeconds)
        {
            WaitForClickable(driver, selector, waitSeconds).SendKeys(keys);
        }

        public static bool WaitToCheckPresenceAndClick(IWebDriver driver, By selector, int waitSeconds)
        {
            try
            {
                WaitUntil(driver, waitSeconds, currentDriver =>
                {
                    currentDriver.FindElement(selector).Click();
                    return true;
                });
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool CheckExistsAndClick(IWebDriver driver, By selector)
        {
            try
            {
                driver.FindElement(selector).Click();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool CheckExists(IWebDriver driver, By selector)
        {
            try
            {
                driver.FindElement(selector);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void WaitUntil(IWebDriver driver, int waitSeconds, Func<IWebDriver, bool> condition)
        {
            var timeout = TimeSpan.FromSeconds(waitSeconds);
            var endTime = DateTime.UtcNow + timeout;
            Exception? lastException = null;

            while (DateTime.UtcNow <= endTime)
            {
                try
                {
                    if (condition(driver))
                    {
                        return;
                    }
                }
                catch (Exception ex) when (ex is NoSuchElementException or StaleElementReferenceException or ElementClickInterceptedException or InvalidOperationException)
                {
                    lastException = ex;
                }

                Thread.Sleep(250);
            }

            throw new TimeoutException("The Selenium wait condition timed out.", lastException);
        }
    }
}

internal static class ConsoleHelper
{
    public static string ReadLineWithPrompt(string message)
    {
        System.Console.Write(message);
        return System.Console.ReadLine() ?? string.Empty;
    }

    public static char[] ReadPasswordWithPrompt(string message)
    {
        System.Console.Write(message);
        var buffer = ArrayPool<char>.Shared.Rent(32);
        var count = 0;

        try
        {
            while (true)
            {
                var key = System.Console.ReadKey(intercept: true);

                if (key.Key == ConsoleKey.Enter)
                {
                    System.Console.WriteLine();
                    return buffer[..count].ToArray();
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (count == 0)
                    {
                        continue;
                    }

                    count -= 1;
                    buffer[count] = '\0';
                    System.Console.Write("\b \b");
                    continue;
                }

                if (char.IsControl(key.KeyChar))
                {
                    continue;
                }

                if (count == buffer.Length)
                {
                    var expandedBuffer = ArrayPool<char>.Shared.Rent(buffer.Length * 2);
                    Array.Copy(buffer, expandedBuffer, buffer.Length);
                    Array.Clear(buffer, 0, buffer.Length);
                    ArrayPool<char>.Shared.Return(buffer);
                    buffer = expandedBuffer;
                }

                buffer[count] = key.KeyChar;
                count += 1;
                System.Console.Write('*');
            }
        }
        finally
        {
            Array.Clear(buffer, 0, buffer.Length);
            ArrayPool<char>.Shared.Return(buffer);
        }
    }
}
