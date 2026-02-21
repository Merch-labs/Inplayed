using System.Windows.Input;

namespace inplayed;

internal sealed class AppConfig
{
	public bool? NativeNvencEnabled { get; init; }
	public HotkeyConfig SaveClipHotkey { get; init; } = new HotkeyConfig();

	public static AppConfig Load() => AppConfigStorage.Load();

	public (ModifierKeys Modifiers, Key Key) GetSaveClipHotkey()
	{
		var modifiers = ParseModifiers(SaveClipHotkey.Modifiers);
		var key = ParseKey(SaveClipHotkey.Key);
		return (modifiers, key);
	}

	private static ModifierKeys ParseModifiers(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return ModifierKeys.Alt;
		}

		var result = ModifierKeys.None;
		var parts = value.Split(new[] { '+', '|', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
		foreach (var part in parts)
		{
			if (Enum.TryParse(part, true, out ModifierKeys parsed))
			{
				result |= parsed;
			}
		}

		return result == ModifierKeys.None ? ModifierKeys.Alt : result;
	}

	private static Key ParseKey(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return Key.F;
		}

		return Enum.TryParse(value, true, out Key parsed) ? parsed : Key.F;
	}
}
