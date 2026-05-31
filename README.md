# Reddit Saved Scalper

## About
Reddit Saved Scalper downloads all media from your Reddit **saved** page. It drives
a real Chrome session with Selenium, so it works with your normal login (including
2FA) and NSFW posts. It's a small Windows desktop app built with .NET MAUI.

Originally a Python script, later a C# console app — now a MAUI GUI.

## Projects
- `src/RedditSavedScalper.Core` — the scraper logic (Selenium): login/session,
  saved-feed collection, gallery extraction, downloads, optional unsave. .NET 8
  class library, UI-agnostic (options in, progress out, cancellable).
- `src/RedditSavedScalper.Maui` — the Windows GUI front-end (`net10.0-windows`).

## Requirements
- .NET 10 SDK and the MAUI Windows workload: `dotnet workload install maui`
- Google Chrome

## Usage
1. Build and run the MAUI app (Visual Studio, or `dotnet build` then launch the exe).
2. Click **Log in to Reddit** once — sign in (and complete any 2FA) in the Chrome
   window. The session is remembered; the app never stores your password.
3. Set your options and press **Start**:
   - **Unsave after download** — remove each post from Saved once it's downloaded.
   - **Run hidden** — scrape with no visible Chrome window (login still shows). On by default.
   - **Download folder** — where media is saved (default: `Documents\Reddit Media`).
   - **Open folder when finished**.
4. Progress streams in the output panel; **Stop** cancels.

## Build
```
dotnet build RedditSavedScalper.slnx
```
(The app must be closed — a running instance locks the Core DLL.)
