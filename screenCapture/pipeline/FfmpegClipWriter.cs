using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Globalization;

public sealed class FfmpegClipWriter : IClipWriter
{
	public Task WriteAsync(string outputPath, EncodedPacketSnapshot snapshot, TimeSpan? maxDuration = null, CancellationToken token = default)
	{
		if (snapshot.Packets.Count == 0)
		{
			return Task.CompletedTask;
		}

		return Task.Run(() => WriteInternal(outputPath, snapshot, maxDuration, token), token);
	}

	private static void WriteInternal(string outputPath, EncodedPacketSnapshot snapshot, TimeSpan? maxDuration, CancellationToken token)
	{
		var outputDir = Path.GetDirectoryName(outputPath);
		if (!string.IsNullOrWhiteSpace(outputDir))
		{
			Directory.CreateDirectory(outputDir);
		}

		try
		{
			var clipFps = ResolveClipFps();
			var ffmpegPath = ResolveFfmpegPath();
			var durationArg = string.Empty;
			if (maxDuration.HasValue && maxDuration.Value > TimeSpan.Zero)
			{
				var seconds = maxDuration.Value.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
				durationArg = $" -t {seconds}";
			}

			var args = $"-y -r {clipFps} -f h264 -i pipe:0 -c copy{durationArg} \"{outputPath}\"";
			var psi = new ProcessStartInfo
			{
				FileName = ffmpegPath,
				Arguments = args,
				UseShellExecute = false,
				RedirectStandardInput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			};

			using var process = Process.Start(psi);
			if (process == null)
			{
				throw new InvalidOperationException("Failed to start ffmpeg process.");
			}

			var pipeClosedByFfmpeg = false;
			var stdin = process.StandardInput.BaseStream;
			try
			{
				foreach (var packet in snapshot.Packets)
				{
					token.ThrowIfCancellationRequested();
					var data = packet.Data.Span;
					try
					{
						stdin.Write(data);
					}
					catch (IOException)
					{
						// Expected when ffmpeg reaches -t and closes stdin early.
						pipeClosedByFfmpeg = true;
						break;
					}
				}

				if (!pipeClosedByFfmpeg)
				{
					stdin.Flush();
				}
			}
			finally
			{
				try
				{
					stdin.Dispose();
				}
				catch (IOException) when (pipeClosedByFfmpeg)
				{
					// Expected when ffmpeg already closed stdin after reaching -t.
				}
				catch (ObjectDisposedException)
				{
				}
			}

			var stderr = process.StandardError.ReadToEnd();
			process.WaitForExit();

			if (process.ExitCode != 0)
			{
				throw new InvalidOperationException($"ffmpeg clip mux failed (exit {process.ExitCode}). {stderr}");
			}
		}
		catch (Win32Exception)
		{
			throw new InvalidOperationException(
				"ffmpeg was not found. Place ffmpeg.exe next to the app, in tools\\ffmpeg\\ffmpeg.exe, or install ffmpeg on PATH.");
		}
	}

	private static int ResolveClipFps()
	{
		var raw = Environment.GetEnvironmentVariable("INPLAYED_CLIP_FPS");
		if (int.TryParse(raw, out var fps) && fps > 0)
		{
			return fps;
		}

		return 60;
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
