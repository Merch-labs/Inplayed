using System.Windows.Input;

namespace inplayed;

internal sealed class AppConfig
{
	public bool? NativeNvencEnabled { get; init; }
	public HotkeyConfig SaveClipHotkey { get; init; } = new();
	public RecordingConfig Recording { get; init; } = new();
	public StartupConfig Startup { get; init; } = new();

	public static AppConfig Load()
	{
		return AppConfigStorage.Load();
	}

	public static AppConfig CreateDefault()
	{
		return AppConfigStorage.CreateDefault();
	}

	public static void Save(AppConfig config)
	{
		AppConfigStorage.Save(config);
	}

	public static string GetConfigPath()
	{
		return AppConfigStorage.GetConfigPath();
	}

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
		var parts = value.Split(['+', '|', ',', ' '], StringSplitOptions.RemoveEmptyEntries);
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

	internal sealed class HotkeyConfig
	{
		public string Modifiers { get; init; } = "Alt";
		public string Key { get; init; } = "F";
	}

	internal sealed class RecordingConfig
	{
		public int Fps { get; init; } = 60;
		public int BitrateMbps { get; init; } = 12;
		public int ClipSeconds { get; init; } = 20;
		public bool IncludeMicAudio { get; init; } = true;
		public bool IncludeSystemAudio { get; init; } = true;
		public CaptureTargetConfig CaptureTarget { get; init; } = new();
		public YamnetDetectionConfig YamnetDetection { get; init; } = new();
	}

	internal sealed class CaptureTargetConfig
	{
		public string Mode { get; init; } = CaptureTargetModes.PrimaryMonitor;
		public int MonitorIndex { get; init; }
		public string ExecutablePath { get; init; } = string.Empty;
	}

	internal sealed class StartupConfig
	{
		public bool LaunchOnWindowsStartup { get; init; }
		public bool StartHiddenOnWindowsStartup { get; init; } = true;
		public bool AutoStartCaptureWhenHiddenLaunch { get; init; } = true;
	}

	internal sealed class YamnetDetectionConfig
	{
		public bool Enabled { get; init; }
		public string ModelPath { get; init; } = string.Empty;
		public int SensitivityPercent { get; init; } = 65;
		public int CooldownSeconds { get; init; } = 15;
	}
}
