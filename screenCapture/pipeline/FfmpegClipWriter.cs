using System.ComponentModel;
using System.Diagnostics;
using System.IO;

public sealed class FfmpegClipWriter : IClipWriter
{
	public Task WriteAsync(string outputPath, EncodedPacketSnapshot snapshot, CancellationToken token = default)
	{
		if (snapshot.Packets.Count == 0)
		{
			return Task.CompletedTask;
		}

		return Task.Run(() => WriteInternal(outputPath, snapshot, token), token);
	}

	private static void WriteInternal(string outputPath, EncodedPacketSnapshot snapshot, CancellationToken token)
	{
		var outputDir = Path.GetDirectoryName(outputPath);
		if (!string.IsNullOrWhiteSpace(outputDir))
		{
			Directory.CreateDirectory(outputDir);
		}

		try
		{
			var ffmpegPath = ResolveFfmpegPath();
			var args = $"-y -f h264 -i pipe:0 -c copy \"{outputPath}\"";
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

			using (var stdin = process.StandardInput.BaseStream)
			{
				foreach (var packet in snapshot.Packets)
				{
					token.ThrowIfCancellationRequested();
					var data = packet.Data.Span;
					stdin.Write(data);
				}

				stdin.Flush();
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
