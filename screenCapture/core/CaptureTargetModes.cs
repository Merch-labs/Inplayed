public static class CaptureTargetModes
{
	public const string PrimaryMonitor = "PrimaryMonitor";
	public const string SpecificMonitor = "SpecificMonitor";
	public const string ActiveWindow = "ActiveWindow";
	public const string ExecutablePath = "ExecutablePath";

	public static bool IsValid(string? value)
	{
		return string.Equals(value, PrimaryMonitor, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(value, SpecificMonitor, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(value, ActiveWindow, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(value, ExecutablePath, StringComparison.OrdinalIgnoreCase);
	}
}
