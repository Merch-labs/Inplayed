using System.ComponentModel;
using System.Diagnostics;
using System.IO;

public sealed class FfmpegEncoder : IVideoEncoder
{
	private readonly RecordingSettings _settings;
	private readonly object _lock = new();
	private readonly Queue<VideoFrame> _frames = new();
	private readonly int _maxFrames;

	public FfmpegEncoder(RecordingSettings settings)
	{
		_settings = settings;
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

		return Task.Run(() => WriteWithFfmpeg(path, frames, _settings));
	}

	public void Dispose() { }

	private static void WriteWithFfmpeg(string path, IReadOnlyList<VideoFrame> frames, RecordingSettings settings)
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

		var fps = Math.Max(1, settings.Fps);
		var bitrate = Math.Max(1, settings.Bitrate);
		var ffmpegPath = ResolveFfmpegPath();
		var args =
			$"-y -f rawvideo -pix_fmt rgba -s {settings.Width}x{settings.Height} -r {fps} " +
			$"-i - -c:v libx264 -pix_fmt yuv420p -b:v {bitrate} \"{path}\"";

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
				var error = process.StandardError.ReadToEnd();
				throw new InvalidOperationException($"ffmpeg failed (exit {process.ExitCode}). {error}");
			}
		}
		catch (Win32Exception)
		{
			throw new InvalidOperationException(
				"ffmpeg was not found. Place ffmpeg.exe next to the app, in tools\\ffmpeg\\ffmpeg.exe, " +
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

		return "ffmpeg";
	}
}
