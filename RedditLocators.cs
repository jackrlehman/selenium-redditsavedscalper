internal static class RedditUrls
{
    public const string Home = "https://www.reddit.com/";

    public static string SavedPage(string username) => $"https://www.reddit.com/user/{username}/saved/";
}

internal static class RedditLocators
{
    internal static class Login
    {
        public const string PopupButtonXpath = "/html/body/div[1]/div/div[2]/div[1]/header/div/div[2]/div/div[1]/a[1]";
        public const string FormIframeXpath = "/html/body/div[1]/div/div[2]/div[3]/div/div/iframe";
        public const string UsernameFieldId = "loginUsername";
        public const string PasswordFieldId = "loginPassword";
        public const string FormButtonXpath = "/html/body/div/main/div[1]/div/div/form/fieldset[4]/button";
        public const string ConfirmationXpath = "/html/body/div[1]/div/div[2]/div[3]/div/div/div[1]";
        public const string ExpectedConfirmationText = "logged in";
        public const string InterestsPopupCloseButtonXpath = "/html/body/div[1]/div/div[2]/div[4]/div/div/div/header/div/div[2]/button/i";
    }

    internal static class Saved
    {
        public const string TableItemXpath = "/html/body/div[1]/div/div[2]/div[2]/div/div/div/div[2]/div[3]/div[1]/div[2]/div[1]/div[{0}]";
        public const string PostContentXpath = "/html/body/div[1]/div/div[2]/div[3]/div/div/div/div[2]/div[1]/div[2]/div[1]/div/div[{0}]/div/a";
        public const string PostContentAlt1Xpath = "/html/body/div[1]/div/div[2]/div[3]/div/div/div/div[2]/div[1]/div[2]/div[1]/div/div[5]/div[3]/div[1]/div/a";
        public const string PostContentAlt2Xpath = "/html/body/div[1]/div/div[2]/div[2]/div/div/div/div[2]/div[3]/div[1]/div[2]/div[1]/div/div[5]/div[3]/div[1]/div/a";
        public const string PostContentAlt3Xpath = "/html/body/div[1]/div/div[2]/div[3]/div/div/div/div[2]/div[1]/div[2]/div[1]/div/div[5]/div/a";
        public const string PostContentAlt4Xpath = "/html/body/div[1]/div/div[2]/div[3]/div/div/div/div[2]/div[1]/div[3]/div[1]/div/div[5]/div/a";
        public const string PostContentAlt5Xpath = "/html/body/div[1]/div/div[2]/div[3]/div/div/div/div[2]/div[1]/div[3]/div[1]/div/div[5]/div[3]/div[1]/div/a";
        public const string PostContentAlt6Xpath = "/html/body/div[1]/div/div[2]/div[3]/div/div/div/div[2]/div[1]/div[3]/div[1]/div/div[6]/div/a";
        public const string PostContentArrayXpath = "/html/body/div[1]/div/div[2]/div[3]/div/div/div/div[2]/div[1]/div[2]/div[1]/div/div/div/div[5]/div[1]/div/div[1]/ul/li[{0}]/figure/a";
        public const string PostContentArrayAlt1Xpath = "/html/body/div[1]/div/div[2]/div[3]/div/div/div/div[2]/div[1]/div[2]/div[1]/div/div[5]/div[3]/div/div[1]/div/div[1]/ul/li[{0}]/figure/a";
        public const string PostContentArrayAlt2Xpath = "/html/body/div[1]/div/div[2]/div[3]/div/div/div/div[2]/div[1]/div[3]/div[1]/div/div/div/div[5]/div[1]/div/div[1]/ul/li[{0}]/figure/a";
        public const string DeletedUser1Xpath = "/html/body/div[1]/div/div[2]/div[2]/div/div/div/div[2]/div[3]/div[1]/div[2]/div[1]/div[{0}]/div/div/div[2]/div/div[2]/div[2]/div[2]/span[2]";
        public const string DeletedUser2Xpath = "/html/body/div[1]/div/div[2]/div[2]/div/div/div/div[2]/div[3]/div[1]/div[2]/div[1]/div[{0}]/div/div/div/div/div[2]/div/div[2]/div[2]/div[2]/span[2]";
        public const string UnsaveButtonXpath = "/html/body/div[1]/div/div[2]/div[2]/div/div/div/div[2]/div[3]/div[1]/div[2]/div[1]/div[{0}]/div/div/div[2]/div/div[2]/div[3]/div[3]/div[3]/button";
        public const string UnsaveButtonAlt1Xpath = "/html/body/div[1]/div/div[2]/div[2]/div/div/div/div[2]/div[3]/div[1]/div[2]/div[1]/div[{0}]/div/div/div/div/div[2]/div/div[2]/div[3]/div[3]/div[3]/button";
        public const string UnsaveButtonAlt2Xpath = "/html/body/div[1]/div/div[2]/div[2]/div/div/div/div[2]/div[3]/div[1]/div[2]/div[1]/div[{0}]/div/div/div/div/div[2]/div/div[2]/div[3]/div[3]/div[2]/button";
        public const string UnsaveButtonAlt3Xpath = "/html/body/div[1]/div/div[2]/div[2]/div/div/div/div[2]/div[3]/div[1]/div[2]/div[1]/div[{0}]/div/div/div[2]/div/div[2]/div[3]/div[3]/div[2]/button";
        public const string PostContentCloseButtonXpath = "/html/body/div[1]/div/div[2]/div[3]/div/div/div/div[1]/div/div[2]/button";

        public static readonly string[] StandardContentAlternateXpaths =
        [
            PostContentAlt1Xpath,
            PostContentAlt2Xpath,
            PostContentAlt3Xpath,
            PostContentAlt4Xpath,
            PostContentAlt5Xpath,
            PostContentAlt6Xpath
        ];

        public static readonly string[] ArrayContentXpaths =
        [
            PostContentArrayXpath,
            PostContentArrayAlt1Xpath,
            PostContentArrayAlt2Xpath
        ];
    }
}
