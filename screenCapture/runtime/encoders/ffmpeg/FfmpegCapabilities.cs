using System.Diagnostics;
using System.IO;

public static class FfmpegCapabilities
{
	public static bool SupportsEncoder(string encoderName)
	{
		if (string.IsNullOrWhiteSpace(encoderName))
		{
			return false;
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
				return false;
			}

			var text = process.StandardOutput.ReadToEnd();
			var err = process.StandardError.ReadToEnd();
			process.WaitForExit();
			if (process.ExitCode != 0)
			{
				return false;
			}

			return text.Contains(encoderName, StringComparison.OrdinalIgnoreCase) ||
				err.Contains(encoderName, StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
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
