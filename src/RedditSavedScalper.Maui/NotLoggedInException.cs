namespace RedditSavedScalper.Maui;

// Thrown when a scrape is started without a valid Reddit session, so the UI can
// reset the login state (re-show the Log in button) rather than just erroring.
internal sealed class NotLoggedInException : Exception
{
    public NotLoggedInException()
        : base("Please log in to Reddit first.")
    {
    }
}
