internal static class RedditUrls
{
    public const string Home = "https://www.reddit.com/";
    public const string Login = "https://www.reddit.com/login/";

    public static string SavedPage(string username) => $"https://www.reddit.com/user/{username}/saved/";
}

internal static class RedditLocators
{
    internal static class Login
    {
        // Reddit's login is its own page now (no header popup, no <iframe>).
        // The username/password fields are <faceplate-text-input> web components
        // whose real <input> lives inside an open shadow root.
        public const string UsernameHostId = "login-username";
        public const string PasswordHostId = "login-password";
        public const string ShadowInputCss = "input";
        public const string SubmitButtonCss = "button.login";

        // Present (with is-logged-in="true") only when a session already exists -
        // used to reuse a persistent-profile session and skip the login form.
        public const string LoggedInHeaderCss = "reddit-header-large[is-logged-in='true']";
    }

    internal static class Saved
    {
        // The saved feed is a <shreddit-feed> of <shreddit-post> web components.
        // Each post exposes its media + metadata as attributes, so there is no need
        // to open posts or walk nested divs anymore.
        public const string PostCss = "shreddit-post";
        public const string PostTypeAttribute = "post-type";
        public const string ContentHrefAttribute = "content-href";
        public const string IdAttribute = "id";
        public const string PermalinkAttribute = "permalink";
        // Carousel images are lazy-loaded thumbnails; used only as a fallback when the
        // post JSON (which has every image at full resolution) is unavailable.
        public const string GalleryImageCss = "gallery-carousel img.media-lightbox-img";

        public const string PostType_Gallery = "gallery";

        // Unsave lives behind the per-post overflow ("...") menu. The menu items are
        // portaled out of <shreddit-post-overflow-menu> and a hidden copy is
        // pre-rendered per post, so the save toggle id is not unique - the click must
        // pick the visible one. The id is the same whether the post is saved or not
        // (only the label flips between "Remove from saved" and "Save").
        public const string OverflowMenuCss = "shreddit-post-overflow-menu";
        public const string OverflowTriggerCss = "button[aria-label='Open user actions']";
        public const string UnsaveItemCss = "li#post-overflow-save [role='menuitem']";
    }
}
