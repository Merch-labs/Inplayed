using System.Text.Json;
using System.IO;
using System.Windows.Input;

namespace inplayed;

internal sealed class AppConfig
{
	public bool? NativeNvencEnabled { get; init; }
	public HotkeyConfig SaveClipHotkey { get; init; } = new();
	private static readonly AppConfig Default = new()
	{
		NativeNvencEnabled = true,
		SaveClipHotkey = new HotkeyConfig
		{
			Modifiers = "Alt",
			Key = "F"
		}
	};

	internal sealed class HotkeyConfig
	{
		public string Modifiers { get; init; } = "Alt";
		public string Key { get; init; } = "F";
	}

	public static AppConfig Load()
	{
		var path = ResolveConfigPath();

		// Always attempt one read first if invalid/corrupt/missing fields, overwrite with defaults and read again.
		for (var attempt = 0; attempt < 2; attempt++)
		{
			if (TryRead(path, out var config))
			{
				return config;
			}

			TryWrite(path, Default);
		}

		return Default;
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
			return Key.F10;
		}

		return Enum.TryParse(value, true, out Key parsed) ? parsed : Key.F10;
	}

	private static bool TryRead(string path, out AppConfig config)
	{
		config = Default;
		try
		{
			using var stream = File.OpenRead(path);
			using var json = JsonDocument.Parse(stream);
			var root = json.RootElement;

			if (!root.TryGetProperty("encoder", out var encoder) || encoder.ValueKind != JsonValueKind.Object)
			{
				return false;
			}

			if (!encoder.TryGetProperty("nativeNvencEnabled", out var nativeNvencElement) ||
				(nativeNvencElement.ValueKind != JsonValueKind.True && nativeNvencElement.ValueKind != JsonValueKind.False))
			{
				return false;
			}

			if (!root.TryGetProperty("hotkeys", out var hotkeys) || hotkeys.ValueKind != JsonValueKind.Object)
			{
				return false;
			}

			if (!hotkeys.TryGetProperty("saveClip", out var saveClip) || saveClip.ValueKind != JsonValueKind.Object)
			{
				return false;
			}

			if (!saveClip.TryGetProperty("modifiers", out var modifiersElement) || modifiersElement.ValueKind != JsonValueKind.String)
			{
				return false;
			}

			if (!saveClip.TryGetProperty("key", out var keyElement) || keyElement.ValueKind != JsonValueKind.String)
			{
				return false;
			}

			var modifiers = modifiersElement.GetString();
			var key = keyElement.GetString();
			if (string.IsNullOrWhiteSpace(modifiers) || string.IsNullOrWhiteSpace(key))
			{
				return false;
			}

			config = new AppConfig
			{
				NativeNvencEnabled = nativeNvencElement.GetBoolean(),
				SaveClipHotkey = new HotkeyConfig
				{
					Modifiers = modifiers,
					Key = key
				}
			};
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static void TryWrite(string path, AppConfig config)
	{
		try
		{
			var dir = Path.GetDirectoryName(path);
			if (!string.IsNullOrWhiteSpace(dir))
			{
				Directory.CreateDirectory(dir);
			}

			using var stream = File.Create(path);
			using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
			writer.WriteStartObject();
			writer.WriteStartObject("encoder");
			writer.WriteBoolean("nativeNvencEnabled", config.NativeNvencEnabled ?? true);
			writer.WriteEndObject();

			writer.WriteStartObject("hotkeys");
			writer.WriteStartObject("saveClip");
			writer.WriteString("modifiers", config.SaveClipHotkey.Modifiers);
			writer.WriteString("key", config.SaveClipHotkey.Key);
			writer.WriteEndObject();
			writer.WriteEndObject();
			writer.WriteEndObject();
			writer.Flush();
		}
		catch
		{
			// keep runtime defaults if file cannot be written
		}
	}

	private static string ResolveConfigPath()
	{
		var candidates = new List<string>
		{
			Path.Combine(AppContext.BaseDirectory, "inplayed.config.json"),
			Path.Combine(Environment.CurrentDirectory, "inplayed.config.json")
		};

		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		for (var i = 0; i < 6 && dir != null; i++)
		{
			candidates.Add(Path.Combine(dir.FullName, "inplayed.config.json"));
			dir = dir.Parent;
		}

		foreach (var path in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
		{
			if (File.Exists(path))
			{
				return path;
			}
		}

		return candidates[0];
	}
}
