using OpenQA.Selenium;

internal sealed class Login
{
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
            // Reuse a persistent-profile session if one exists - avoids re-login and 2FA.
            if (HasExistingSession())
            {
                Console.WriteLine("Existing Reddit session detected; skipping login.");
                return;
            }

            driver.Navigate().GoToUrl(RedditUrls.Login);

            TypeIntoShadowInput(RedditLocators.Login.UsernameHostId, username);
            TypeIntoShadowInput(RedditLocators.Login.PasswordHostId, new string(password));

            // Blur the password field so Reddit's change-based validation enables
            // the submit button (verified: button is disabled until this fires).
            driver.FindElement(By.Id(RedditLocators.Login.PasswordHostId))
                .GetShadowRoot()
                .FindElement(By.CssSelector(RedditLocators.Login.ShadowInputCss))
                .SendKeys(Keys.Tab);

            WaitHelper.WaitToBeClickableAndClick(driver, By.CssSelector(RedditLocators.Login.SubmitButtonCss), RedditAppSettings.MediumWaitSeconds);

            // Login has completed once Reddit redirects away from the login page.
            WaitHelper.WaitUntil(driver, RedditAppSettings.LongWaitSeconds, currentDriver =>
                !currentDriver.Url.Contains("/login", StringComparison.OrdinalIgnoreCase));

            Console.WriteLine("Login Successful");
        }
        catch
        {
            Console.WriteLine("Login Failed");
            throw new Exception("Login Failed");
        }
    }

    // True when the browser already holds a logged-in Reddit session (e.g. from a
    // persistent --profile directory), letting us skip the login form entirely.
    private bool HasExistingSession()
    {
        driver.Navigate().GoToUrl(RedditUrls.Home);
        try
        {
            WaitHelper.WaitUntil(driver, RedditAppSettings.ShortWaitSeconds, currentDriver =>
                currentDriver.FindElement(By.CssSelector(RedditLocators.Login.LoggedInHeaderCss)) != null);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    // The login inputs are <faceplate-text-input> web components; the real <input>
    // is inside an open shadow root, so we descend into it before sending keys.
    private void TypeIntoShadowInput(string hostId, string text)
    {
        WaitHelper.WaitUntil(driver, RedditAppSettings.ShortWaitSeconds, currentDriver =>
        {
            try
            {
                var input = currentDriver.FindElement(By.Id(hostId))
                    .GetShadowRoot()
                    .FindElement(By.CssSelector(RedditLocators.Login.ShadowInputCss));

                if (!input.Displayed || !input.Enabled)
                {
                    return false;
                }

                input.SendKeys(text);
                return true;
            }
            catch (WebDriverException)
            {
                return false;
            }
        });
    }
}
