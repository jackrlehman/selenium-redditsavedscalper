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
            WaitHelper.WaitToBeClickableAndClick(driver, By.XPath(RedditLocators.Login.PopupButtonXpath), RedditAppSettings.ShortWaitSeconds);
            WaitHelper.WaitUntil(driver, RedditAppSettings.ShortWaitSeconds, currentDriver =>
            {
                currentDriver.SwitchTo().DefaultContent();
                var frame = currentDriver.FindElement(By.XPath(RedditLocators.Login.FormIframeXpath));
                currentDriver.SwitchTo().Frame(frame);
                return true;
            });

            WaitHelper.WaitToBeClickableAndSendKeys(driver, By.Id(RedditLocators.Login.UsernameFieldId), username, RedditAppSettings.ShortWaitSeconds);
            WaitHelper.WaitToBeClickableAndSendKeys(driver, By.Id(RedditLocators.Login.PasswordFieldId), new string(password), RedditAppSettings.ShortWaitSeconds);

            WaitHelper.WaitToBeClickableAndClick(driver, By.XPath(RedditLocators.Login.FormButtonXpath), RedditAppSettings.ShortWaitSeconds);
            WaitHelper.WaitUntil(driver, RedditAppSettings.MediumWaitSeconds, currentDriver =>
                currentDriver.FindElement(By.XPath(RedditLocators.Login.ConfirmationXpath)).Text.Contains(RedditLocators.Login.ExpectedConfirmationText, StringComparison.OrdinalIgnoreCase));

            driver.SwitchTo().DefaultContent();
            WaitHelper.WaitToCheckPresenceAndClick(driver, By.XPath(RedditLocators.Login.InterestsPopupCloseButtonXpath), RedditAppSettings.MediumWaitSeconds);
            driver.Navigate().GoToUrl(RedditUrls.SavedPage(username));
            Console.WriteLine("Login Successful");
        }
        catch
        {
            Console.WriteLine("Login Failed");
            throw new Exception("Login Failed");
        }
    }
}
