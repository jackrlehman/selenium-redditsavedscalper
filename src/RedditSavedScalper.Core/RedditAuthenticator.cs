using OpenQA.Selenium;

namespace RedditSavedScalper.Core;

// Handles getting the browser into a logged-in state. Two paths:
//  - IsLoggedIn / GetLoggedInUsername: reuse an existing (persistent-profile) session.
//  - WaitForManualLogin:               user logs in by hand in the Chrome window.
// Login state is detected via /api/me.json (fetched with the session cookies), which
// is far more reliable than scraping a header element across page/hydration states.
// There is no typed-password path: logging in is always done by the user in the real
// Chrome window, which also handles 2FA.
public sealed class RedditAuthenticator
{
    private readonly IWebDriver driver;

    public RedditAuthenticator(IWebDriver driver)
    {
        this.driver = driver;
    }

    public bool IsLoggedIn() => GetLoggedInUsername() != null;

    // Reads the logged-in username from the session, so neither front-end has to ask
    // for it. Returns null if not logged in / it can't be determined.
    public string? GetLoggedInUsername()
    {
        driver.Navigate().GoToUrl(RedditUrls.Home);
        return ReadUsernameOnCurrentPage();
    }

    // Opens the login page and polls until the user has logged in (including any 2FA)
    // in the Chrome window, or the timeout / cancellation hits. Polls a background
    // fetch (no navigation), so the user is never pulled off the login form. Persists
    // the session when a persistent profile is in use. Never handles a password.
    public bool WaitForManualLogin(int timeoutSeconds, Action<string>? log, CancellationToken cancellationToken)
    {
        driver.Navigate().GoToUrl(RedditUrls.Login);
        log?.Invoke("Waiting for login in the Chrome window (complete any 2FA there)...");

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var user = ReadUsernameOnCurrentPage();
            if (!string.IsNullOrEmpty(user))
            {
                log?.Invoke($"Login detected (u/{user}).");
                return true;
            }

            Thread.Sleep(1500);
        }

        return false;
    }

    // Fetches the current user's name from /api/me.json on the CURRENT page (no
    // navigation, so it is safe to call in a polling loop). Returns null when logged
    // out, mid-navigation, or on any error.
    private string? ReadUsernameOnCurrentPage()
    {
        try
        {
            driver.Manage().Timeouts().AsynchronousJavaScript = TimeSpan.FromSeconds(RedditAppSettings.ShortWaitSeconds);

            var name = ((IJavaScriptExecutor)driver).ExecuteAsyncScript(@"
                const cb = arguments[arguments.length - 1];
                fetch('/api/me.json', {credentials:'include'})
                  .then(r => r.json())
                  .then(d => cb((d && d.data && d.data.name) || (d && d.name) || ''))
                  .catch(() => cb(''));
            ") as string;

            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch (WebDriverException)
        {
            return null;
        }
    }
}
