using OpenQA.Selenium;

// TEMPORARY diagnostic helper. Drives a manual DOM-capture session so the live
// Reddit markup can be saved to disk and used to refresh the (stale) selectors in
// RedditLocators.cs. Run with:  dotnet run -- --capture-dom
// Safe to delete once the selectors have been updated.
internal static class DomCapture
{
    // Opens the login page in a persistent-profile browser and waits while you log
    // in manually (including any 2FA). Seeds the session cookies so later --profile
    // runs skip login entirely. The app never handles your password here - you type
    // it straight into Reddit.
    public static void SeedProfile(IWebDriver driver)
    {
        driver.Navigate().GoToUrl(RedditUrls.Login);
        Console.WriteLine();
        Console.WriteLine("Log in (and complete any 2FA) in the Chrome window, then press Enter here.");
        Console.ReadLine();
        Console.WriteLine("Session seeded. Future runs with --profile will reuse this login.");
    }

    // Non-interactive: capture the public pages (home + login) that need no auth.
    // Lets the login-flow selectors be refreshed without a logged-in session.
    public static void RunPublic(IWebDriver driver)
    {
        var outputDir = Path.Combine(Environment.CurrentDirectory, "dom-samples", "captured");
        Directory.CreateDirectory(outputDir);
        Console.WriteLine($"Auto-capturing public Reddit pages to:\n  {outputDir}");

        driver.Navigate().GoToUrl(RedditUrls.Home);
        Thread.Sleep(5000);
        Dump(driver, outputDir, "home.html");

        driver.Navigate().GoToUrl("https://www.reddit.com/login/");
        Thread.Sleep(5000);
        Dump(driver, outputDir, "04-login-modal.html");
    }

    // Non-interactive probe: report how the login inputs are structured (shadow
    // DOM open/closed, where the real <input> lives) so Login.cs can target them.
    public static void ProbeLogin(IWebDriver driver)
    {
        driver.Navigate().GoToUrl("https://www.reddit.com/login/");
        Thread.Sleep(5000);

        var result = ((IJavaScriptExecutor)driver).ExecuteScript(@"
            const u = document.getElementById('login-username');
            const p = document.getElementById('login-password');
            const submit = document.querySelector('button.login');
            const probe = (el) => {
                if (!el) return 'MISSING';
                const sr = el.shadowRoot;
                const inner = sr ? sr.querySelector('input') : el.querySelector('input');
                return {
                    tag: el.tagName,
                    hasOpenShadow: !!sr,
                    innerInput: inner ? (inner.tagName + ' type=' + (inner.getAttribute('type')||'')) : 'none',
                };
            };
            return JSON.stringify({
                username: probe(u),
                password: probe(p),
                submitButton: submit ? (submit.className.split('\n')[0].trim() + ' | text=' + submit.textContent.trim()) : 'MISSING',
            });
        ");

        Console.WriteLine("LOGIN PROBE: " + result);
    }

    // Non-interactive: validate the exact Selenium path Login.cs will use -
    // host element -> open shadow root -> inner <input> -> SendKeys -> read back.
    public static void TestLoginTyping(IWebDriver driver)
    {
        driver.Navigate().GoToUrl("https://www.reddit.com/login/");
        Thread.Sleep(5000);

        var userInput = driver.FindElement(By.Id("login-username")).GetShadowRoot().FindElement(By.CssSelector("input"));
        userInput.SendKeys("test_user_value");

        var passInput = driver.FindElement(By.Id("login-password")).GetShadowRoot().FindElement(By.CssSelector("input"));
        passInput.SendKeys("test_pass_value");

        var submit = driver.FindElement(By.CssSelector("button.login"));
        var enabledBeforeBlur = submit.Enabled;

        // Blur the password field to trigger the form's change-based validation.
        passInput.SendKeys(Keys.Tab);
        Thread.Sleep(1500);

        Console.WriteLine($"TYPING TEST: username='{userInput.GetAttribute("value")}' " +
                          $"password='{passInput.GetAttribute("value")}' " +
                          $"submitEnabledBeforeBlur={enabledBeforeBlur} submitEnabledAfterBlur={submit.Enabled}");
    }

    // Read-only: compares how many gallery images the DOM carousel exposes versus
    // how many the post actually has (from Reddit's post JSON, fetched with the
    // session cookies so NSFW galleries resolve too).
    public static void ProbeGallery(IWebDriver driver, string username, char[] password)
    {
        new Login(driver, username, password).PerformLogin();
        driver.Navigate().GoToUrl(RedditUrls.SavedPage(username));
        Thread.Sleep(5000);
        driver.Manage().Timeouts().AsynchronousJavaScript = TimeSpan.FromSeconds(20);

        var gallery = driver.FindElements(By.CssSelector(RedditLocators.Saved.PostCss))
            .FirstOrDefault(p => p.GetAttribute(RedditLocators.Saved.PostTypeAttribute) == RedditLocators.Saved.PostType_Gallery);
        if (gallery == null)
        {
            Console.WriteLine("GALLERY PROBE: no gallery post found in feed.");
            return;
        }

        ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", gallery);
        Thread.Sleep(1000);

        var permalink = gallery.GetAttribute("permalink");
        var carouselCount = gallery.FindElements(By.CssSelector(RedditLocators.Saved.GalleryImageCss)).Count;

        var json = ((IJavaScriptExecutor)driver).ExecuteAsyncScript(@"
            const cb = arguments[arguments.length-1];
            fetch(arguments[0] + '.json', {credentials:'include'})
              .then(r => r.json())
              .then(d => {
                  const p = d[0].data.children[0].data;
                  const mm = p.media_metadata || {};
                  const order = (p.gallery_data && p.gallery_data.items) || [];
                  const urls = order.map(it => {
                      const m = mm[it.media_id]; if(!m) return null;
                      const ext = (m.m||'').split('/')[1] || 'jpg';
                      return 'https://i.redd.it/' + it.media_id + '.' + ext;
                  }).filter(Boolean);
                  cb(JSON.stringify({jsonCount: urls.length, urls: urls.slice(0, 25)}));
              })
              .catch(e => cb('ERR: ' + e));
        ", permalink) as string;

        Console.WriteLine($"GALLERY PROBE: permalink={permalink}");
        Console.WriteLine($"  carousel images in DOM: {carouselCount}");
        Console.WriteLine($"  post JSON: {json}");
    }

    // Read-only: opens a post's overflow menu and deep-searches (through open shadow
    // roots) for the "Unsave" item, reporting enough to write a stable selector.
    public static void ProbeMenu(IWebDriver driver, string username, char[] password)
    {
        new Login(driver, username, password).PerformLogin();
        driver.Navigate().GoToUrl(RedditUrls.SavedPage(username));
        Thread.Sleep(5000);

        var trigger = driver.FindElement(By.CssSelector(
            $"{RedditLocators.Saved.OverflowMenuCss} {RedditLocators.Saved.OverflowTriggerCss}"));
        var js = (IJavaScriptExecutor)driver;
        js.ExecuteScript("arguments[0].scrollIntoView({block:'center'});", trigger);
        Thread.Sleep(500);
        try { trigger.Click(); }
        catch (ElementNotInteractableException) { js.ExecuteScript("arguments[0].click();", trigger); }
        Thread.Sleep(4000);

        var result = js.ExecuteScript(@"
            function* walk(root){ for(const el of root.querySelectorAll('*')){ yield el; if(el.shadowRoot) yield* walk(el.shadowRoot); } }
            const items=[]; const seen=new Set();
            for(const el of walk(document)){
                if(el.getAttribute('role')==='menuitem'){
                    const li=el.closest('li');
                    const liId=li?li.id:null;
                    const text=(el.textContent||'').replace(/\s+/g,' ').trim();
                    const key=liId+'|'+text;
                    if(seen.has(key)) continue; seen.add(key);
                    items.push({text, liId, ariaLabel:el.getAttribute('aria-label')});
                }
            }
            return JSON.stringify(items);
        ");
        Console.WriteLine("MENU PROBE: " + result);
    }

    // Read-only dry run: logs in, verifies the collector finds every saved post
    // (prints count + sample URLs, downloads nothing), then opens the first post's
    // overflow menu and dumps it so the "Unsave" selector can be written from real
    // DOM. Opening a menu does not modify the account.
    public static void DryRunSaved(IWebDriver driver, string username, char[] password)
    {
        var outputDir = Path.Combine(Environment.CurrentDirectory, "dom-samples", "captured");
        Directory.CreateDirectory(outputDir);

        new Login(driver, username, password).PerformLogin();

        var items = new SavedScraper(driver, username, false).CollectSavedItems();
        var allUrls = items.SelectMany(i => i.MediaUrls).ToList();
        Console.WriteLine($"COLLECTOR: {items.Count} posts; {allUrls.Count} media URLs " +
                          $"({allUrls.Distinct().Count()} distinct).");
        foreach (var item in items)
        {
            Console.WriteLine($"  {item.PostId} [{item.PostType}] -> {item.MediaUrls.Count} url(s)");
        }

        // After scrolling to the bottom, how many posts remain in the DOM? If this is
        // far below the collector total, the feed virtualizes and unsave-by-id would
        // skip off-screen posts.
        var liveCount = ((IJavaScriptExecutor)driver).ExecuteScript(
            "return document.querySelectorAll('shreddit-post').length");
        Console.WriteLine($"VIRTUALIZATION CHECK: {liveCount} shreddit-post still in DOM at bottom.");

        // Open a post's overflow menu and capture the rendered menu. The collector
        // left us scrolled to the bottom, so bring the trigger into view first.
        var trigger = driver.FindElement(By.CssSelector(
            $"{RedditLocators.Saved.OverflowMenuCss} {RedditLocators.Saved.OverflowTriggerCss}"));
        var js = (IJavaScriptExecutor)driver;
        js.ExecuteScript("arguments[0].scrollIntoView({block:'center'});", trigger);
        Thread.Sleep(500);
        try
        {
            trigger.Click();
        }
        catch (ElementNotInteractableException)
        {
            js.ExecuteScript("arguments[0].click();", trigger);
        }
        Thread.Sleep(2000);
        Dump(driver, outputDir, "02-unsave-menu.html");
        Console.WriteLine("Captured open overflow menu.");
    }

    // Logs in (real credentials, entered at the console) and dumps the saved feed
    // so the Saved selectors can be refreshed against authenticated markup.
    public static void CaptureSaved(IWebDriver driver, string username, char[] password)
    {
        var outputDir = Path.Combine(Environment.CurrentDirectory, "dom-samples", "captured");
        Directory.CreateDirectory(outputDir);

        new Login(driver, username, password).PerformLogin();
        driver.Navigate().GoToUrl(RedditUrls.SavedPage(username));
        Thread.Sleep(6000); // let the saved feed render
        Dump(driver, outputDir, "01-saved-card.html");
        Console.WriteLine($"Saved feed captured to {outputDir}");
    }

    public static void Run(IWebDriver driver, string username)
    {
        var outputDir = Path.Combine(Environment.CurrentDirectory, "dom-samples", "captured");
        Directory.CreateDirectory(outputDir);

        Console.WriteLine();
        Console.WriteLine($"DOM capture mode. Snapshots will be written to:\n  {outputDir}");
        Console.WriteLine("A Selenium-controlled Chrome window is open. Perform each step IN THAT WINDOW,");
        Console.WriteLine("then return to this console and press Enter to snapshot the current page.");

        driver.Navigate().GoToUrl(RedditUrls.Home);

        Pause("STEP 1/4 - In the Chrome window, click 'Log In' so the login form is visible. Then press Enter.");
        Dump(driver, outputDir, "04-login-modal.html");

        Pause("STEP 2/4 - Finish logging in manually (credentials, any captcha/2FA). Once logged in, press Enter.");
        driver.Navigate().GoToUrl(RedditUrls.SavedPage(username));
        Pause("           Your Saved page is loading - wait until saved posts are visible, then press Enter.");
        Dump(driver, outputDir, "01-saved-card.html");

        Pause("STEP 3/4 - Click one saved post to open it (image/video + close button visible). Then press Enter.");
        Dump(driver, outputDir, "03-post-expanded.html");

        Pause("STEP 4/4 - Close the post. Click the '...' overflow menu on a saved post so 'Unsave' is visible. Then press Enter.");
        Dump(driver, outputDir, "02-unsave-menu.html");

        Console.WriteLine();
        Console.WriteLine("DOM capture complete. You can close the Chrome window.");
    }

    private static void Pause(string message)
    {
        Console.WriteLine();
        Console.WriteLine(message);
        Console.ReadLine();
    }

    private static void Dump(IWebDriver driver, string outputDir, string fileName)
    {
        // Prefer getHTML() so open (declarative) shadow roots of shreddit-* web
        // components are serialized too; fall back to outerHTML / PageSource.
        var html = ((IJavaScriptExecutor)driver).ExecuteScript(
            "return document.documentElement.getHTML" +
            " ? document.documentElement.getHTML({serializableShadowRoots:true})" +
            " : document.documentElement.outerHTML") as string ?? driver.PageSource;

        var path = Path.Combine(outputDir, fileName);
        File.WriteAllText(path, html);
        Console.WriteLine($"  -> saved {fileName} ({new FileInfo(path).Length:n0} bytes)");
    }
}
