using OpenQA.Selenium;

internal static class WaitHelper
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
