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
			var defaultClipFps = ResolveClipFps();
			var clipFps = ResolveClipFps(snapshot, maxDuration, defaultClipFps);
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

	private static int ResolveClipFps(EncodedPacketSnapshot snapshot, TimeSpan? maxDuration, int fallbackFps)
	{
		var frameCount = CountAccessUnits(snapshot.Packets);
		if (frameCount <= 0)
		{
			return fallbackFps;
		}

		if (maxDuration.HasValue && maxDuration.Value > TimeSpan.Zero)
		{
			var targetSeconds = maxDuration.Value.TotalSeconds;
			var targetFps = (int)Math.Round(frameCount / targetSeconds, MidpointRounding.AwayFromZero);
			return Math.Clamp(targetFps, 1, 240);
		}

		var firstTs = long.MaxValue;
		var lastTs = long.MinValue;
		foreach (var packet in snapshot.Packets)
		{
			var ts = packet.PresentationTimestamp;
			if (ts <= 0)
			{
				continue;
			}

			if (ts < firstTs)
			{
				firstTs = ts;
			}

			if (ts > lastTs)
			{
				lastTs = ts;
			}
		}

		if (firstTs == long.MaxValue || lastTs <= firstTs)
		{
			return fallbackFps;
		}

		var seconds = (lastTs - firstTs) / 1000.0;
		if (seconds <= 0.01)
		{
			return fallbackFps;
		}

		var estimated = (int)Math.Round(frameCount / seconds, MidpointRounding.AwayFromZero);
		return Math.Clamp(estimated, 1, 240);
	}

	private static int CountAccessUnits(IReadOnlyList<EncodedPacket> packets)
	{
		var frames = 0;
		for (var i = 0; i < packets.Count; i++)
		{
			var nalType = GetNalType(packets[i].Data.Span);
			if (nalType is >= 1 and <= 5)
			{
				frames++;
			}
		}

		return frames;
	}

	private static int GetNalType(ReadOnlySpan<byte> data)
	{
		if (data.Length < 5)
		{
			return -1;
		}

		var idx = 0;
		if (data[0] == 0x00 && data[1] == 0x00 && data[2] == 0x01)
		{
			idx = 3;
		}
		else if (data[0] == 0x00 && data[1] == 0x00 && data[2] == 0x00 && data[3] == 0x01)
		{
			idx = 4;
		}
		else
		{
			return -1;
		}

		return data[idx] & 0x1F;
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
