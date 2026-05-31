using Microsoft.Maui.Storage;

namespace RedditSavedScalper.Maui;

// Last line of defense: any exception that escapes a handler is written to a log
// file AND surfaced as a dialog, so the app can never fail silently.
internal static class CrashReporter
{
    public static string LogPath => Path.Combine(FileSystem.AppDataDirectory, "crash.log");

    public static void Hook()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Report(e.ExceptionObject as Exception, "AppDomain");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Report(e.Exception, "UnobservedTask");
            e.SetObserved();
        };
    }

    public static void Report(Exception? exception, string source)
    {
        var message = exception?.Message ?? "Unknown error";

        try
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now:O}] {source}: {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Crash logging must never itself throw.
        }

        try
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
                if (page is not null)
                {
                    await page.DisplayAlertAsync("Unexpected error", $"{message}\n\nDetails saved to:\n{LogPath}", "OK");
                }
            });
        }
        catch
        {
            // Best-effort surfacing; the file log above is the durable record.
        }
    }
}
