namespace inplayed;

internal sealed class AppLaunchOptions
{
	public const string BackgroundArgument = "--background";

	public bool StartHidden { get; init; }
	public bool AutoStartCapture { get; init; }

	public static AppLaunchOptions From(string[] args, AppConfig config)
	{
		var hiddenLaunchRequested = args.Any(arg => string.Equals(arg, BackgroundArgument, StringComparison.OrdinalIgnoreCase));
		if (!hiddenLaunchRequested)
		{
			return new AppLaunchOptions();
		}

		return new AppLaunchOptions
		{
			StartHidden = config.Startup.StartHiddenOnWindowsStartup,
			AutoStartCapture = config.Startup.AutoStartCaptureWhenHiddenLaunch
		};
	}
}
