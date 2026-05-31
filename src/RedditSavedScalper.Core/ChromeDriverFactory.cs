using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace RedditSavedScalper.Core;

public static class ChromeDriverFactory
{
    public static ChromeDriver Create(ScraperOptions options)
    {
        var chromeOptions = new ChromeOptions();
        // Return once the DOM is parsed instead of waiting for every image/ad/socket
        // to finish - Reddit's media-heavy pages otherwise keep the load event pending
        // and block navigation. The scraper waits for the posts it needs explicitly.
        chromeOptions.PageLoadStrategy = PageLoadStrategy.Eager;
        chromeOptions.AddArgument("--disable-infobars");
        chromeOptions.AddArgument("start-maximized");
        chromeOptions.AddArgument("--disable-extensions");
        chromeOptions.AddExcludedArgument("enable-logging");
        chromeOptions.AddUserProfilePreference("profile.default_content_setting_values.notifications", 1);

        if (options.Headless)
        {
            chromeOptions.AddArgument("--headless=new");
            // Headless has no real window; an explicit size is required or the saved
            // feed's lazy-loading and scroll paging break.
            chromeOptions.AddArgument("--window-size=1920,1080");
            chromeOptions.AddArgument("--disable-gpu");

            // Reddit/Cloudflare serves headless browsers a logged-out/challenge page,
            // so strip the obvious headless fingerprints: the "HeadlessChrome" UA and
            // the automation flags. (UA version is cosmetic; bump if Chrome drifts far.)
            chromeOptions.AddArgument("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/148.0.0.0 Safari/537.36");
            chromeOptions.AddArgument("--disable-blink-features=AutomationControlled");
            chromeOptions.AddExcludedArgument("enable-automation");
        }

        if (!string.IsNullOrWhiteSpace(options.ProfileDirectory))
        {
            Directory.CreateDirectory(options.ProfileDirectory);
            chromeOptions.AddArgument($"--user-data-dir={Path.GetFullPath(options.ProfileDirectory)}");
        }

        var driver = new ChromeDriver(chromeOptions);
        driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(RedditAppSettings.PageLoadTimeoutSeconds);
        return driver;
    }
}
