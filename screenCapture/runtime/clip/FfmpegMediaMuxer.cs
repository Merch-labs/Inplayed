using System.ComponentModel;
using System.Diagnostics;
using System.IO;

internal sealed class FfmpegMediaMuxer
{
	public async Task MuxAsync(
		string videoPath,
		string outputPath,
		string? micAudioPath,
		string? systemAudioPath,
		CancellationToken token = default)
	{
		var args = BuildArguments(videoPath, outputPath, micAudioPath, systemAudioPath);
		if (args == null)
		{
			throw new InvalidOperationException("No audio inputs were provided for muxing.");
		}

		var outputDir = Path.GetDirectoryName(outputPath);
		if (!string.IsNullOrWhiteSpace(outputDir))
		{
			Directory.CreateDirectory(outputDir);
		}

		var psi = new ProcessStartInfo
		{
			FileName = FfmpegPathResolver.ResolvePath(),
			Arguments = args,
			UseShellExecute = false,
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

			var stderrTask = process.StandardError.ReadToEndAsync(token);
			await process.WaitForExitAsync(token);
			var stderr = await stderrTask;
			if (process.ExitCode != 0)
			{
				throw new InvalidOperationException($"ffmpeg media mux failed (exit {process.ExitCode}). {stderr}");
			}
		}
		catch (Win32Exception)
		{
			throw new InvalidOperationException(
				"ffmpeg was not found. Place ffmpeg.exe next to the app, in tools\\ffmpeg\\ffmpeg.exe, or install ffmpeg on PATH.");
		}
	}

	internal static string? BuildArguments(
		string videoPath,
		string outputPath,
		string? micAudioPath,
		string? systemAudioPath)
	{
		var inputs = new List<string>();
		if (!string.IsNullOrWhiteSpace(micAudioPath) && File.Exists(micAudioPath))
		{
			inputs.Add(micAudioPath);
		}

		if (!string.IsNullOrWhiteSpace(systemAudioPath) && File.Exists(systemAudioPath))
		{
			inputs.Add(systemAudioPath);
		}

		if (inputs.Count == 0)
		{
			return null;
		}

		var quotedVideo = Quote(videoPath);
		var quotedOutput = Quote(outputPath);
		if (inputs.Count == 1)
		{
			return $"-y -i {quotedVideo} -i {Quote(inputs[0])} -map 0:v:0 -map 1:a:0 -c:v copy -c:a aac -b:a 192k -shortest -movflags +faststart {quotedOutput}";
		}

		return $"-y -i {quotedVideo} -i {Quote(inputs[0])} -i {Quote(inputs[1])} " +
			"-filter_complex \"[1:a][2:a]amix=inputs=2:normalize=0,aresample=async=1:first_pts=0[aout]\" " +
			$"-map 0:v:0 -map \"[aout]\" -c:v copy -c:a aac -b:a 192k -shortest -movflags +faststart {quotedOutput}";
	}

	private static string Quote(string path)
	{
		return $"\"{path}\"";
	}
}
