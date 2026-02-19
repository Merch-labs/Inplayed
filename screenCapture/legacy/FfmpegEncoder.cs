using System.ComponentModel;
using System.Diagnostics;
using System.IO;

public sealed class FfmpegEncoder : IVideoEncoder
{
	private readonly RecordingSettings _settings;
	private readonly string _videoCodec;
	private readonly object _lock = new();
	private readonly Queue<VideoFrame> _frames = new();
	private readonly int _maxFrames;

	public FfmpegEncoder(RecordingSettings settings, string videoCodec = "libx264")
	{
		_settings = settings;
		_videoCodec = string.IsNullOrWhiteSpace(videoCodec) ? "libx264" : videoCodec;
		var fps = Math.Max(1, settings.Fps);
		_maxFrames = Math.Max(1, fps * settings.ClipSeconds);
	}

	public void PushFrame(VideoFrame frame)
	{
		lock (_lock)
		{
			_frames.Enqueue(frame);
			while (_frames.Count > _maxFrames)
			{
				_frames.Dequeue();
			}
		}
	}

	public Task FlushRecentAsync(string path, TimeSpan clipLength)
	{
		List<VideoFrame> frames;
		var fps = Math.Max(1, _settings.Fps);
		var expectedFrames = Math.Max(1, (int)Math.Round(clipLength.TotalSeconds * fps));
		double? fpsOverride = null;
		lock (_lock)
		{
			if (_frames.Count == 0)
			{
				return Task.CompletedTask;
			}

			var all = _frames.ToArray();
			var latestTimestamp = all[^1].Timestamp;
			var earliest = latestTimestamp - (long)clipLength.TotalMilliseconds;
			frames = all.Where(f => f.Timestamp >= earliest).ToList();
		}

		if (frames.Count > 1)
		{
			var startTimestamp = frames[0].Timestamp;
			var endTimestamp = frames[^1].Timestamp;
			var targetDurationMs = Math.Max(1, (long)clipLength.TotalMilliseconds);
			var actualDurationMs = Math.Max(1, endTimestamp - startTimestamp);

			if (actualDurationMs < targetDurationMs)
			{
				// Clip is shorter than requested; keep frames as-is and adjust fps to avoid freeze/padding.
				fpsOverride = frames.Count / (actualDurationMs / 1000.0);
			}
			else
			{
				var targetStart = endTimestamp - targetDurationMs;
				var frameDurationMs = targetDurationMs / (double)expectedFrames;

				var resampled = new List<VideoFrame>(expectedFrames);
				var index = 0;
				for (var i = 0; i < expectedFrames; i++)
				{
					var targetTime = targetStart + (long)Math.Round(i * frameDurationMs);
					while (index + 1 < frames.Count && frames[index + 1].Timestamp <= targetTime)
					{
						index++;
					}

					resampled.Add(frames[index]);
				}

				frames = resampled;
			}
		}

		return Task.Run(() => WriteWithFfmpeg(path, frames, _settings, fpsOverride));
	}

	public void Dispose() { }

	private void WriteWithFfmpeg(
		string path,
		IReadOnlyList<VideoFrame> frames,
		RecordingSettings settings,
		double? fpsOverride)
	{
		if (frames.Count == 0)
		{
			return;
		}

		var directory = Path.GetDirectoryName(path);
		if (!string.IsNullOrWhiteSpace(directory))
		{
			Directory.CreateDirectory(directory);
		}

		var fps = Math.Max(1.0, fpsOverride ?? settings.Fps);
		var bitrate = Math.Max(1, settings.Bitrate);
		var ffmpegPath = ResolveFfmpegPath();
		var args =
			$"-y -f rawvideo -pix_fmt bgra -s {settings.Width}x{settings.Height} -r {fps} " +
			$"-i - -c:v {_videoCodec} -pix_fmt yuv420p -b:v {bitrate} \"{path}\"";

		var psi = new ProcessStartInfo
		{
			FileName = ffmpegPath,
			Arguments = args,
			UseShellExecute = false,
			RedirectStandardInput = true,
			RedirectStandardError = true,
			CreateNoWindow = true
		};

		try
		{
			using var process = Process.Start(psi);
			if (process == null)
			{
				throw new InvalidOperationException("Failed to start ffmpeg process.");
			}

			var errorTask = process.StandardError.ReadToEndAsync();

			using var stdin = process.StandardInput.BaseStream;
			foreach (var frame in frames)
			{
				stdin.Write(frame.Data, 0, frame.Data.Length);
			}

			stdin.Flush();
			process.StandardInput.Close();
			process.WaitForExit();

			if (process.ExitCode != 0)
			{
				var error = errorTask.GetAwaiter().GetResult();
				throw new InvalidOperationException($"ffmpeg failed (exit {process.ExitCode}). {error}");
			}
		}
		catch (Win32Exception)
		{
			throw new InvalidOperationException(
				"ffmpeg was not found. Run scripts\\download-ffmpeg.ps1, place ffmpeg.exe next to the app, " +
				"or install ffmpeg and ensure it is available on PATH.");
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

		var searchDir = new DirectoryInfo(baseDir);
		string? repoRoot = null;
		while (searchDir != null)
		{
			var repoTools = Path.Combine(searchDir.FullName, "tools", "ffmpeg", "ffmpeg.exe");
			if (File.Exists(repoTools))
			{
				return repoTools;
			}

			if (repoRoot == null && File.Exists(Path.Combine(searchDir.FullName, "inplayed.csproj")))
			{
				repoRoot = searchDir.FullName;
			}

			searchDir = searchDir.Parent;
		}

		if (!string.IsNullOrWhiteSpace(repoRoot))
		{
			TryDownloadFfmpeg(repoRoot);
			var repoTools = Path.Combine(repoRoot, "tools", "ffmpeg", "ffmpeg.exe");
			if (File.Exists(repoTools))
			{
				return repoTools;
			}
		}

		return "ffmpeg";
	}

	private static void TryDownloadFfmpeg(string repoRoot)
	{
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
			// Swallow and let normal ffmpeg resolution continue/fail with existing error message.
		}
	}
}
