using System.Diagnostics;
using System.IO;
using System.Threading;

public static class FfmpegPathResolver
{
	private static int _downloadAttempted;

	public static string Resolve()
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

		var repoRoot = FindRepoRoot(baseDir);
		if (!string.IsNullOrWhiteSpace(repoRoot))
		{
			var repoTools = Path.Combine(repoRoot, "tools", "ffmpeg", "ffmpeg.exe");
			if (File.Exists(repoTools))
			{
				return repoTools;
			}

			TryRunDownloadScript(repoRoot);
			if (File.Exists(repoTools))
			{
				return repoTools;
			}
		}

		return "ffmpeg";
	}

	public static string MissingMessage =>
		"ffmpeg was not found. Run scripts\\download-ffmpeg.ps1, place ffmpeg.exe next to the app, or install ffmpeg on PATH.";

	private static string? FindRepoRoot(string startDir)
	{
		var dir = new DirectoryInfo(startDir);
		while (dir != null)
		{
			if (File.Exists(Path.Combine(dir.FullName, "inplayed.csproj")))
			{
				return dir.FullName;
			}

			dir = dir.Parent;
		}

		return null;
	}

	private static void TryRunDownloadScript(string repoRoot)
	{
		if (Interlocked.Exchange(ref _downloadAttempted, 1) != 0)
		{
			return;
		}

		try
		{
			var scriptPath = Path.Combine(repoRoot, "scripts", "download-ffmpeg.ps1");
			if (!File.Exists(scriptPath))
			{
				return;
			}

			var psi = new ProcessStartInfo
			{
				FileName = "powershell",
				Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
				UseShellExecute = false,
				CreateNoWindow = true,
				WorkingDirectory = repoRoot
			};

			using var process = Process.Start(psi);
			process?.WaitForExit();
		}
		catch
		{
		}
	}
}
