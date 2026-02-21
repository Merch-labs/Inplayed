namespace inplayed;

internal sealed class HotkeyConfig
{
	// Default to Alt+F if invalid or missing.
	public string Modifiers { get; init; } = "Alt";
	public string Key { get; init; } = "F";
}
