using System.Text.Json;
using System.IO;

namespace inplayed;

internal sealed class AppConfig
{
	public bool? NativeNvencEnabled { get; init; }

	public static AppConfig Load()
	{
		var path = FindConfigPath();
		if (path == null)
		{
			return new AppConfig();
		}

		try
		{
			using var stream = File.OpenRead(path);
			using var json = JsonDocument.Parse(stream);
			var root = json.RootElement;
			if (!root.TryGetProperty("encoder", out var encoder) || encoder.ValueKind != JsonValueKind.Object)
			{
				return new AppConfig();
			}

			bool? nativeNvencEnabled = null;
			if (encoder.TryGetProperty("nativeNvencEnabled", out var nativeNvencElement) &&
				(nativeNvencElement.ValueKind == JsonValueKind.True || nativeNvencElement.ValueKind == JsonValueKind.False))
			{
				nativeNvencEnabled = nativeNvencElement.GetBoolean();
			}

			return new AppConfig
			{
				NativeNvencEnabled = nativeNvencEnabled
			};
		}
		catch
		{
			return new AppConfig();
		}
	}

	private static string? FindConfigPath()
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

		return null;
	}
}
