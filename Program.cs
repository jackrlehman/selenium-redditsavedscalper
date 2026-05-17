using OpenQA.Selenium.Chrome;

internal static class Program
{
    private static async Task Main()
    {
        Console.WriteLine("Welcome to Reddit Saved Scalper by jackrlehman. Converted to C# Selenium. The scalper will download all images from your reddit saved page. Please ensure you have Google Chrome installed on this device and do not interfere with the newly opened Chrome application.");

        try
        {
            using var driver = new ChromeDriver(CreateChromeOptions());

            try
            {
                var username = AppPrompts.Prompt("Enter Reddit Username: ");
                var password = AppPrompts.PromptPassword("Enter Reddit Password: ");

                try
                {
                    var unsavePostAfterDownload = AppPrompts.PromptForUnsavePreference();

                    Console.WriteLine("Process started. You will find a folder called 'Reddit Media' containing the downloaded contents in your base directory.");

                    driver.Navigate().GoToUrl(RedditUrls.Home);
                    new Login(driver, username, password).PerformLogin();
                    await new SavedScraper(driver, username, unsavePostAfterDownload).BeginAsync();
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

    private static ChromeOptions CreateChromeOptions()
    {
        var options = new ChromeOptions();
        options.AddArgument("--disable-infobars");
        options.AddArgument("start-maximized");
        options.AddArgument("--disable-extensions");
        options.AddExcludedArgument("enable-logging");
        options.AddUserProfilePreference("profile.default_content_setting_values.notifications", 1);
        return options;
    }
}
