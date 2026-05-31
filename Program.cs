using OpenQA.Selenium.Chrome;

internal static class Program
{
    // When set (via --profile), Chrome persists cookies/cache here so the login
    // session survives between runs - skipping login and 2FA on later runs.
    private static string? profileDir;

    private static async Task Main(string[] args)
    {
        Console.WriteLine("Welcome to Reddit Saved Scalper by jackrlehman. Converted to C# Selenium. The scalper will download all images from your reddit saved page. Please ensure you have Google Chrome installed on this device and do not interfere with the newly opened Chrome application.");

        profileDir = GetProfileDir(args);
        if (profileDir != null)
        {
            Console.WriteLine($"Using persistent Chrome profile: {profileDir}");
        }

        if (args.Contains("--login", StringComparer.OrdinalIgnoreCase))
        {
            // Seeding a session only makes sense with a persistent profile.
            profileDir ??= Path.Combine(Environment.CurrentDirectory, ".selenium-profile");
            RunWithDriver(DomCapture.SeedProfile);
            return;
        }

        if (args.Contains("--probe-login", StringComparer.OrdinalIgnoreCase))
        {
            RunWithDriver(DomCapture.ProbeLogin);
            return;
        }

        if (args.Contains("--test-typing", StringComparer.OrdinalIgnoreCase))
        {
            RunWithDriver(DomCapture.TestLoginTyping);
            return;
        }

        if (args.Contains("--capture-public", StringComparer.OrdinalIgnoreCase))
        {
            RunDomCapturePublic();
            return;
        }

        if (args.Contains("--capture-saved", StringComparer.OrdinalIgnoreCase))
        {
            RunDomCaptureSaved();
            return;
        }

        if (args.Contains("--dry-run-saved", StringComparer.OrdinalIgnoreCase))
        {
            RunDomCaptureSaved(DomCapture.DryRunSaved);
            return;
        }

        if (args.Contains("--probe-menu", StringComparer.OrdinalIgnoreCase))
        {
            RunDomCaptureSaved(DomCapture.ProbeMenu);
            return;
        }

        if (args.Contains("--probe-gallery", StringComparer.OrdinalIgnoreCase))
        {
            RunDomCaptureSaved(DomCapture.ProbeGallery);
            return;
        }

        if (args.Contains("--capture-dom", StringComparer.OrdinalIgnoreCase))
        {
            RunDomCapture();
            return;
        }

        try
        {
            using var driver = new ChromeDriver(CreateChromeOptions());

            try
            {
                var username = AppPrompts.Prompt("Enter Reddit Username: ");
                // A persistent profile already holds the session, so no password is needed.
                char[] password = profileDir != null ? [] : AppPrompts.PromptPassword("Enter Reddit Password: ");

                try
                {
                    var unsavePostAfterDownload = AppPrompts.PromptForUnsavePreference();

                    Console.WriteLine("Process started. You will find a folder called 'Reddit Media' containing the downloaded contents in your base directory.");

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

    // TEMPORARY: non-interactive capture of public pages (home + login). No auth needed.
    private static void RunDomCapturePublic() => RunWithDriver(DomCapture.RunPublic);

    // TEMPORARY: log in and run an authenticated capture (needs your credentials, so run it yourself).
    private static void RunDomCaptureSaved(Action<ChromeDriver, string, char[]>? action = null)
    {
        action ??= DomCapture.CaptureSaved;
        try
        {
            using var driver = new ChromeDriver(CreateChromeOptions());
            var username = AppPrompts.Prompt("Enter Reddit Username: ");
            // A persistent profile already holds the session, so no password is needed.
            var password = profileDir != null ? [] : AppPrompts.PromptPassword("Enter Reddit Password: ");
            try
            {
                action(driver, username, password);
            }
            finally
            {
                Array.Clear(password, 0, password.Length);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Capture failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void RunWithDriver(Action<ChromeDriver> action)
    {
        try
        {
            using var driver = new ChromeDriver(CreateChromeOptions());
            action(driver);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Capture failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // TEMPORARY: capture live Reddit DOM to refresh stale selectors. Remove with DomCapture.cs.
    private static void RunDomCapture()
    {
        try
        {
            using var driver = new ChromeDriver(CreateChromeOptions());
            var username = AppPrompts.Prompt("Enter Reddit Username (used to build your saved page URL): ");
            DomCapture.Run(driver, username);
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

        if (!string.IsNullOrWhiteSpace(profileDir))
        {
            Directory.CreateDirectory(profileDir);
            options.AddArgument($"--user-data-dir={Path.GetFullPath(profileDir)}");
        }

        return options;
    }

    // Parses an optional persistent-profile directory from the args:
    //   --profile            -> default folder (.selenium-profile in the app dir)
    //   --profile=<path>     -> a specific folder
    // Returns null when not requested (a fresh, throwaway browser session).
    private static string? GetProfileDir(string[] args)
    {
        const string defaultDir = ".selenium-profile";

        foreach (var arg in args)
        {
            if (arg.Equals("--profile", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(Environment.CurrentDirectory, defaultDir);
            }

            if (arg.StartsWith("--profile=", StringComparison.OrdinalIgnoreCase))
            {
                var value = arg["--profile=".Length..].Trim('"');
                return string.IsNullOrWhiteSpace(value) ? Path.Combine(Environment.CurrentDirectory, defaultDir) : value;
            }
        }

        return null;
    }
}
