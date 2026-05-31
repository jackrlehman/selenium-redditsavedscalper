using RedditSavedScalper.Core;
using RedditSavedScalper.Maui.Services;

namespace RedditSavedScalper.Maui;

public partial class MainPage : ContentPage
{
    private CancellationTokenSource? cancellation;
    private bool busy;
    private bool revealed;

    public MainPage()
    {
        InitializeComponent();
        LoadSettings();
        LogEditor.Text = "Ready. Press Start to download your saved posts.";
    }

    // One tasteful staggered reveal on first load (fade + slight rise).
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (revealed)
        {
            return;
        }

        revealed = true;
        var sections = new View[] { HeaderSection, OptionsCard, ActionsSection, LogSection };
        foreach (var section in sections)
        {
            section.Opacity = 0;
            section.TranslationY = 16;
        }

        await Task.Delay(40);
        foreach (var section in sections)
        {
            _ = section.FadeTo(1, 340, Easing.CubicOut);
            _ = section.TranslateTo(0, 0, 340, Easing.CubicOut);
            await Task.Delay(80);
        }
    }

    private void LoadSettings()
    {
        UnsaveSwitch.IsToggled = AppSettings.UnsaveAfterDownload;
        HeadlessSwitch.IsToggled = AppSettings.Headless;
        FolderEntry.Text = AppSettings.DownloadFolder;
        OpenWhenDoneSwitch.IsToggled = AppSettings.OpenFolderWhenDone;
        UpdateLoginStatus();
    }

    private void UpdateLoginStatus()
    {
        var user = AppSettings.LastLoggedInUser;
        var loggedIn = !string.IsNullOrEmpty(user);
        LoginStatusLabel.Text = loggedIn ? $"Logged in as u/{user}" : "Not logged in";
        LoginStatusDot.Fill = new SolidColorBrush(Color.FromArgb(loggedIn ? "#3DD9B0" : "#8B949E"));
        // Once logged in the button is just clutter; it returns if the session lapses.
        LoginButton.IsVisible = !loggedIn;
    }

    // --- Settings persistence (write-through on change) ---
    private void OnFolderChanged(object? sender, TextChangedEventArgs e) => AppSettings.DownloadFolder = FolderEntry.Text ?? string.Empty;
    private void OnUnsaveToggled(object? sender, ToggledEventArgs e) => AppSettings.UnsaveAfterDownload = e.Value;
    private void OnHeadlessToggled(object? sender, ToggledEventArgs e) => AppSettings.Headless = e.Value;
    private void OnOpenWhenDoneToggled(object? sender, ToggledEventArgs e) => AppSettings.OpenFolderWhenDone = e.Value;

    private string ResolvedDownloadFolder() =>
        string.IsNullOrWhiteSpace(FolderEntry.Text) ? AppSettings.DefaultDownloadFolder : FolderEntry.Text!.Trim();

    // --- Login (manual; seeds the session and learns the username from it) ---
    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        if (busy)
        {
            return;
        }

        var profile = AppSettings.ProfileDirectory;
        SetBusy(true);
        Log("Opening Chrome for login. Complete it (and any 2FA) in that window...");

        try
        {
            var user = await Task.Run(() =>
            {
                using var driver = ChromeDriverFactory.Create(new ScraperOptions { Username = string.Empty, ProfileDirectory = profile });
                var auth = new RedditAuthenticator(driver);
                var ok = auth.WaitForManualLogin(180, message => MainThread.BeginInvokeOnMainThread(() => Log(message)), CancellationToken.None);
                return ok ? auth.GetLoggedInUsername() : null;
            });

            if (!string.IsNullOrEmpty(user))
            {
                AppSettings.LastLoggedInUser = user;
                UpdateLoginStatus();
                Log($"Logged in as u/{user}. You can Start now.");
            }
            else
            {
                await ShowErrorAsync("Login was not detected within the timeout. Please try again.");
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Login failed: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    // --- Run / Stop ---
    private async void OnStartClicked(object? sender, EventArgs e)
    {
        if (busy)
        {
            return;
        }

        // Capture UI values on the UI thread; the username comes from the session.
        var unsave = UnsaveSwitch.IsToggled;
        var useHeadless = HeadlessSwitch.IsToggled;
        var folder = ResolvedDownloadFolder();
        var profile = AppSettings.ProfileDirectory;
        var openWhenDone = OpenWhenDoneSwitch.IsToggled;

        cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var progress = new Progress<ScrapeProgress>(OnProgress);

        SetBusy(true);
        LogEditor.Text = string.Empty;
        CountLabel.Text = string.Empty;
        Log("Starting...");

        try
        {
            var result = await Task.Run(async () =>
            {
                using var driver = ChromeDriverFactory.Create(new ScraperOptions { Username = string.Empty, ProfileDirectory = profile, Headless = useHeadless });
                var auth = new RedditAuthenticator(driver);
                if (!auth.IsLoggedIn())
                {
                    throw new NotLoggedInException();
                }

                var user = auth.GetLoggedInUsername()
                    ?? throw new InvalidOperationException("Couldn't read your username from the session.");

                var options = new ScraperOptions
                {
                    Username = user,
                    UnsaveAfterDownload = unsave,
                    DownloadFolder = folder,
                    ProfileDirectory = profile,
                };

                return await new SavedScraper(driver, options, progress, token).RunAsync();
            }, token);

            Log($"Finished: {result.FilesDownloaded} files from {result.PostsFound} posts ({result.Skipped} skipped).");

            if (openWhenDone)
            {
                OpenFolder(folder);
            }
        }
        catch (OperationCanceledException)
        {
            Log("Stopped.");
        }
        catch (NotLoggedInException ex)
        {
            // Session is gone (or never existed) - reset so the Log in button returns.
            AppSettings.LastLoggedInUser = string.Empty;
            UpdateLoginStatus();
            await ShowErrorAsync(ex.Message);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Could not finish: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
            cancellation?.Dispose();
            cancellation = null;
        }
    }

    private void OnStopClicked(object? sender, EventArgs e)
    {
        Log("Stopping...");
        cancellation?.Cancel();
    }

    private void OnProgress(ScrapeProgress progress)
    {
        Log(progress.Message);
        CountLabel.Text = $"{progress.FilesDownloaded} files / {progress.PostsProcessed} posts";
    }

    // --- Folder picker (Windows) ---
    private async void OnBrowseClicked(object? sender, EventArgs e)
    {
#if WINDOWS
        try
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.FileTypeFilter.Add("*");
            var platformWindow = App.Current?.Windows?[0]?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(platformWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                FolderEntry.Text = folder.Path;
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Folder picker unavailable ({ex.Message}); type a path instead.");
        }
#else
        await Task.CompletedTask;
#endif
    }

    // --- Helpers ---
    private void Log(string message) => LogEditor.Text += message + Environment.NewLine;

    // Surface failures both in the log and as a dialog, so nothing fails silently.
    private async Task ShowErrorAsync(string message)
    {
        Log(message);
        await DisplayAlertAsync("Reddit Saved Scalper", message, "OK");
    }

    private void SetBusy(bool value)
    {
        busy = value;
        StartButton.IsEnabled = !value;
        LoginButton.IsEnabled = !value;
        StopButton.IsEnabled = value;
    }

    private void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Log($"Could not open the download folder: {ex.Message}");
        }
    }
}
