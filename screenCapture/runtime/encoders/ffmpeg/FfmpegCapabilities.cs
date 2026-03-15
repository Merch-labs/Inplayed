using System.Diagnostics;
using System.IO;

public static class FfmpegCapabilities
{
	private static readonly object _encodersGate = new();
	private static string? _cachedEncoderListing;

	public static bool SupportsEncoder(string encoderName)
	{
		if (string.IsNullOrWhiteSpace(encoderName))
		{
			return false;
		}

		var listing = GetEncoderListing();
		if (string.IsNullOrWhiteSpace(listing))
		{
			return false;
		}

		return ContainsEncoderListing(listing, encoderName);
	}

	internal static bool ContainsEncoderListing(string listing, string encoderName)
	{
		if (string.IsNullOrWhiteSpace(listing) || string.IsNullOrWhiteSpace(encoderName))
		{
			return false;
		}

		return listing.Contains(encoderName, StringComparison.OrdinalIgnoreCase);
	}

	private static string? GetEncoderListing()
	{
		lock (_encodersGate)
		{
			if (!string.IsNullOrWhiteSpace(_cachedEncoderListing))
			{
				return _cachedEncoderListing;
			}
		}

		try
		{
			var ffmpegPath = ResolveFfmpegPath();
			var psi = new ProcessStartInfo
			{
				FileName = ffmpegPath,
				Arguments = "-hide_banner -encoders",
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			};

			using var process = Process.Start(psi);
			if (process == null)
			{
				return null;
			}

			var text = process.StandardOutput.ReadToEnd();
			var err = process.StandardError.ReadToEnd();
			process.WaitForExit();
			if (process.ExitCode != 0)
			{
				return null;
			}

			var listing = $"{text}{Environment.NewLine}{err}";
			lock (_encodersGate)
			{
				if (string.IsNullOrWhiteSpace(_cachedEncoderListing))
				{
					_cachedEncoderListing = listing;
				}

				return _cachedEncoderListing;
			}
		}
		catch
		{
			return null;
		}
	}

	private static string ResolveFfmpegPath()
	{
		var baseDir = AppContext.BaseDirectory;
		var local = Path.Combine(baseDir, "ffmpeg.exe");
		if (File.Exists(local))
		{
			return local;
		}

		var tools = Path.Combine(baseDir, "tools", "ffmpeg", "ffmpeg.exe");
		if (File.Exists(tools))
		{
			return tools;
		}

		return "ffmpeg";
	}
}
