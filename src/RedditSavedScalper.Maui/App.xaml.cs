using Microsoft.Extensions.DependencyInjection;

namespace RedditSavedScalper.Maui;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		CrashReporter.Hook();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// Host MainPage directly (no Shell chrome) with a sensible desktop size.
		return new Window(new MainPage())
		{
			Title = "Reddit Saved Scalper",
			Width = 580,
			Height = 780,
			MinimumWidth = 520,
			MinimumHeight = 660,
		};
	}
}