using System.IO;
using System.Text.Json;

namespace inplayed;

internal static class AppConfigStorage
{
	private static readonly AppConfig Default = new()
	{
		NativeNvencEnabled = true,
		SaveClipHotkey = new AppConfig.HotkeyConfig
		{
			Modifiers = "Alt",
			Key = "F"
		},
		Recording = new AppConfig.RecordingConfig
		{
			Fps = 60,
			BitrateMbps = 12,
			ClipSeconds = 20,
			IncludeMicAudio = true,
			IncludeSystemAudio = true,
			CaptureTarget = new AppConfig.CaptureTargetConfig
			{
				Mode = CaptureTargetModes.PrimaryMonitor,
				MonitorIndex = 0,
				ExecutablePath = string.Empty
			},
			YamnetDetection = new AppConfig.YamnetDetectionConfig
			{
				Enabled = false,
				ModelPath = string.Empty,
				SensitivityPercent = 65,
				CooldownSeconds = 15
			}
		},
		Startup = new AppConfig.StartupConfig
		{
			LaunchOnWindowsStartup = false,
			StartHiddenOnWindowsStartup = true,
			AutoStartCaptureWhenHiddenLaunch = true
		}
	};

	public static AppConfig Load()
	{
		var path = ResolveConfigPath();

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

	public static AppConfig CreateDefault()
	{
		return new AppConfig
		{
			NativeNvencEnabled = Default.NativeNvencEnabled,
			SaveClipHotkey = new AppConfig.HotkeyConfig
			{
				Modifiers = Default.SaveClipHotkey.Modifiers,
				Key = Default.SaveClipHotkey.Key
			},
			Recording = new AppConfig.RecordingConfig
			{
				Fps = Default.Recording.Fps,
				BitrateMbps = Default.Recording.BitrateMbps,
				ClipSeconds = Default.Recording.ClipSeconds,
				IncludeMicAudio = Default.Recording.IncludeMicAudio,
				IncludeSystemAudio = Default.Recording.IncludeSystemAudio,
				CaptureTarget = new AppConfig.CaptureTargetConfig
				{
					Mode = Default.Recording.CaptureTarget.Mode,
					MonitorIndex = Default.Recording.CaptureTarget.MonitorIndex,
					ExecutablePath = Default.Recording.CaptureTarget.ExecutablePath
				},
				YamnetDetection = new AppConfig.YamnetDetectionConfig
				{
					Enabled = Default.Recording.YamnetDetection.Enabled,
					ModelPath = Default.Recording.YamnetDetection.ModelPath,
					SensitivityPercent = Default.Recording.YamnetDetection.SensitivityPercent,
					CooldownSeconds = Default.Recording.YamnetDetection.CooldownSeconds
				}
			},
			Startup = new AppConfig.StartupConfig
			{
				LaunchOnWindowsStartup = Default.Startup.LaunchOnWindowsStartup,
				StartHiddenOnWindowsStartup = Default.Startup.StartHiddenOnWindowsStartup,
				AutoStartCaptureWhenHiddenLaunch = Default.Startup.AutoStartCaptureWhenHiddenLaunch
			}
		};
	}

	public static void Save(AppConfig config)
	{
		TryWrite(ResolveConfigPath(), config);
	}

	public static string GetConfigPath()
	{
		return ResolveConfigPath();
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

			if (!root.TryGetProperty("recording", out var recording) || recording.ValueKind != JsonValueKind.Object)
			{
				return false;
			}

			if (!hotkeys.TryGetProperty("saveClip", out var saveClip) || saveClip.ValueKind != JsonValueKind.Object)
			{
				return false;
			}

			if (!TryReadString(saveClip, "modifiers", out var modifiers) || string.IsNullOrWhiteSpace(modifiers))
			{
				return false;
			}

			if (!TryReadString(saveClip, "key", out var key) || string.IsNullOrWhiteSpace(key))
			{
				return false;
			}

			if (!TryReadInt(recording, "fps", 24, 240, out var fps))
			{
				return false;
			}

			if (!TryReadInt(recording, "bitrateMbps", 1, 200, out var bitrateMbps))
			{
				return false;
			}

			if (!TryReadInt(recording, "clipSeconds", 5, 300, out var clipSeconds))
			{
				return false;
			}

			if (!TryReadBool(recording, "includeMicAudio", out var includeMicAudio))
			{
				return false;
			}

			if (!TryReadBool(recording, "includeSystemAudio", out var includeSystemAudio))
			{
				return false;
			}

			if (!TryReadCaptureTarget(recording, out var captureTarget))
			{
				return false;
			}

			var yamnetDetection = TryReadYamnetDetection(recording, out var parsedYamnetDetection)
				? parsedYamnetDetection
				: new AppConfig.YamnetDetectionConfig
				{
					Enabled = Default.Recording.YamnetDetection.Enabled,
					ModelPath = Default.Recording.YamnetDetection.ModelPath,
					SensitivityPercent = Default.Recording.YamnetDetection.SensitivityPercent,
					CooldownSeconds = Default.Recording.YamnetDetection.CooldownSeconds
				};

			var startup = TryReadStartup(root, out var parsedStartup)
				? parsedStartup
				: new AppConfig.StartupConfig
				{
					LaunchOnWindowsStartup = Default.Startup.LaunchOnWindowsStartup,
					StartHiddenOnWindowsStartup = Default.Startup.StartHiddenOnWindowsStartup,
					AutoStartCaptureWhenHiddenLaunch = Default.Startup.AutoStartCaptureWhenHiddenLaunch
				};

			config = new AppConfig
			{
				NativeNvencEnabled = nativeNvencElement.GetBoolean(),
				SaveClipHotkey = new AppConfig.HotkeyConfig
				{
					Modifiers = modifiers,
					Key = key
				},
				Recording = new AppConfig.RecordingConfig
				{
					Fps = fps,
					BitrateMbps = bitrateMbps,
					ClipSeconds = clipSeconds,
					IncludeMicAudio = includeMicAudio,
					IncludeSystemAudio = includeSystemAudio,
					CaptureTarget = captureTarget,
					YamnetDetection = yamnetDetection
				},
				Startup = startup
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

			writer.WriteStartObject("recording");
			writer.WriteNumber("fps", config.Recording.Fps);
			writer.WriteNumber("bitrateMbps", config.Recording.BitrateMbps);
			writer.WriteNumber("clipSeconds", config.Recording.ClipSeconds);
			writer.WriteBoolean("includeMicAudio", config.Recording.IncludeMicAudio);
			writer.WriteBoolean("includeSystemAudio", config.Recording.IncludeSystemAudio);
			writer.WriteStartObject("captureTarget");
			writer.WriteString("mode", config.Recording.CaptureTarget.Mode);
			writer.WriteNumber("monitorIndex", config.Recording.CaptureTarget.MonitorIndex);
			writer.WriteString("executablePath", config.Recording.CaptureTarget.ExecutablePath);
			writer.WriteEndObject();
			writer.WriteStartObject("yamnetDetection");
			writer.WriteBoolean("enabled", config.Recording.YamnetDetection.Enabled);
			writer.WriteString("modelPath", config.Recording.YamnetDetection.ModelPath);
			writer.WriteNumber("sensitivityPercent", config.Recording.YamnetDetection.SensitivityPercent);
			writer.WriteNumber("cooldownSeconds", config.Recording.YamnetDetection.CooldownSeconds);
			writer.WriteEndObject();
			writer.WriteEndObject();

			writer.WriteStartObject("startup");
			writer.WriteBoolean("launchOnWindowsStartup", config.Startup.LaunchOnWindowsStartup);
			writer.WriteBoolean("startHiddenOnWindowsStartup", config.Startup.StartHiddenOnWindowsStartup);
			writer.WriteBoolean("autoStartCaptureWhenHiddenLaunch", config.Startup.AutoStartCaptureWhenHiddenLaunch);
			writer.WriteEndObject();

			writer.WriteEndObject();
			writer.Flush();
		}
		catch
		{
		}
	}

	private static bool TryReadBool(JsonElement parent, string propertyName, out bool value)
	{
		value = false;
		if (!parent.TryGetProperty(propertyName, out var element))
		{
			return false;
		}

		if (element.ValueKind != JsonValueKind.True && element.ValueKind != JsonValueKind.False)
		{
			return false;
		}

		value = element.GetBoolean();
		return true;
	}

	private static bool TryReadCaptureTarget(JsonElement recording, out AppConfig.CaptureTargetConfig captureTarget)
	{
		captureTarget = new AppConfig.CaptureTargetConfig();
		if (!recording.TryGetProperty("captureTarget", out var targetElement) || targetElement.ValueKind != JsonValueKind.Object)
		{
			return false;
		}

		if (!TryReadString(targetElement, "mode", out var mode) || !CaptureTargetModes.IsValid(mode))
		{
			return false;
		}

		if (!TryReadInt(targetElement, "monitorIndex", 0, 32, out var monitorIndex))
		{
			return false;
		}

		if (!TryReadString(targetElement, "executablePath", out var executablePath))
		{
			return false;
		}

		captureTarget = new AppConfig.CaptureTargetConfig
		{
			Mode = mode,
			MonitorIndex = monitorIndex,
			ExecutablePath = executablePath
		};
		return true;
	}

	private static bool TryReadYamnetDetection(JsonElement recording, out AppConfig.YamnetDetectionConfig yamnetDetection)
	{
		yamnetDetection = new AppConfig.YamnetDetectionConfig
		{
			Enabled = Default.Recording.YamnetDetection.Enabled,
			ModelPath = Default.Recording.YamnetDetection.ModelPath,
			SensitivityPercent = Default.Recording.YamnetDetection.SensitivityPercent,
			CooldownSeconds = Default.Recording.YamnetDetection.CooldownSeconds
		};

		if (!recording.TryGetProperty("yamnetDetection", out var yamnetElement))
		{
			return false;
		}

		if (yamnetElement.ValueKind != JsonValueKind.Object)
		{
			return false;
		}

		if (!TryReadBool(yamnetElement, "enabled", out var enabled))
		{
			return false;
		}

		if (!TryReadString(yamnetElement, "modelPath", out var modelPath))
		{
			return false;
		}

		var sensitivityPercent = Default.Recording.YamnetDetection.SensitivityPercent;
		if (yamnetElement.TryGetProperty("sensitivityPercent", out _)
			&& !TryReadInt(yamnetElement, "sensitivityPercent", 1, 100, out sensitivityPercent))
		{
			return false;
		}

		var cooldownSeconds = Default.Recording.YamnetDetection.CooldownSeconds;
		if (yamnetElement.TryGetProperty("cooldownSeconds", out _)
			&& !TryReadInt(yamnetElement, "cooldownSeconds", 5, 120, out cooldownSeconds))
		{
			return false;
		}

		yamnetDetection = new AppConfig.YamnetDetectionConfig
		{
			Enabled = enabled,
			ModelPath = modelPath,
			SensitivityPercent = sensitivityPercent,
			CooldownSeconds = cooldownSeconds
		};
		return true;
	}

	private static bool TryReadStartup(JsonElement root, out AppConfig.StartupConfig startup)
	{
		startup = new AppConfig.StartupConfig
		{
			LaunchOnWindowsStartup = Default.Startup.LaunchOnWindowsStartup,
			StartHiddenOnWindowsStartup = Default.Startup.StartHiddenOnWindowsStartup,
			AutoStartCaptureWhenHiddenLaunch = Default.Startup.AutoStartCaptureWhenHiddenLaunch
		};

		if (!root.TryGetProperty("startup", out var startupElement))
		{
			return false;
		}

		if (startupElement.ValueKind != JsonValueKind.Object)
		{
			return false;
		}

		if (!TryReadBool(startupElement, "launchOnWindowsStartup", out var launchOnWindowsStartup))
		{
			return false;
		}

		var startHiddenOnWindowsStartup = Default.Startup.StartHiddenOnWindowsStartup;
		if (startupElement.TryGetProperty("startHiddenOnWindowsStartup", out _)
			&& !TryReadBool(startupElement, "startHiddenOnWindowsStartup", out startHiddenOnWindowsStartup))
		{
			return false;
		}

		var autoStartCaptureWhenHiddenLaunch = Default.Startup.AutoStartCaptureWhenHiddenLaunch;
		if (startupElement.TryGetProperty("autoStartCaptureWhenHiddenLaunch", out _)
			&& !TryReadBool(startupElement, "autoStartCaptureWhenHiddenLaunch", out autoStartCaptureWhenHiddenLaunch))
		{
			return false;
		}

		startup = new AppConfig.StartupConfig
		{
			LaunchOnWindowsStartup = launchOnWindowsStartup,
			StartHiddenOnWindowsStartup = startHiddenOnWindowsStartup,
			AutoStartCaptureWhenHiddenLaunch = autoStartCaptureWhenHiddenLaunch
		};
		return true;
	}

	private static bool TryReadInt(JsonElement parent, string propertyName, int min, int max, out int value)
	{
		value = 0;
		if (!parent.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.Number)
		{
			return false;
		}

		if (!element.TryGetInt32(out var parsed))
		{
			return false;
		}

		if (parsed < min || parsed > max)
		{
			return false;
		}

		value = parsed;
		return true;
	}

	private static bool TryReadString(JsonElement parent, string propertyName, out string value)
	{
		value = string.Empty;
		if (!parent.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.String)
		{
			return false;
		}

		value = element.GetString() ?? string.Empty;
		return true;
	}

	private static string ResolveConfigPath()
	{
		var candidates = new List<string>
		{
			Path.Combine(AppContext.BaseDirectory, "config", "inplayed.config.json"),
			Path.Combine(Environment.CurrentDirectory, "config", "inplayed.config.json"),
			Path.Combine(AppContext.BaseDirectory, "inplayed.config.json"),
			Path.Combine(Environment.CurrentDirectory, "inplayed.config.json")
		};

		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		for (var i = 0; i < 6 && dir != null; i++)
		{
			candidates.Add(Path.Combine(dir.FullName, "config", "inplayed.config.json"));
			candidates.Add(Path.Combine(dir.FullName, "inplayed.config.json"));
			dir = dir.Parent;
		}

		var resolvedPath = PathAlgorithms.FindFirstExistingDistinctPath(candidates);
		if (!string.IsNullOrWhiteSpace(resolvedPath))
		{
			return resolvedPath;
		}

		return candidates[0];
	}
}
