using System.Drawing;
using System.Windows.Forms;

namespace inplayed;

public sealed class CaptureController : IDisposable
{
	private readonly object _sync = new();
	private readonly FfmpegMediaMuxer _mediaMuxer = new();
	private ClipSession? _session;
	private string _lastSessionStatus = "stopped";
	private readonly AudioRecorder _micRecorder = new();
	private readonly AudioRecorder _systemRecorder = new();
	private bool _disposed;
	private Bitmap? _latestPreviewFrame;

	public Task StartCapture()
	{
		ThrowIfDisposed();
		lock (_sync)
		{
			if (_session != null)
			{
				return Task.CompletedTask;
			}

			var settings = CreateDefaultSettings();
			var session = new ClipSession(settings);
			session.StatusChanged += OnSessionStatusChanged;
			session.PreviewFrameReady += OnPreviewFrameReady;
			StartAudioRecording(settings.ClipSeconds);
			return StartSessionAsync(session);
		}
	}

	public Bitmap? GetPreviewFrame(int maxWidth, int maxHeight)
	{
		if (_disposed)
		{
			return null;
		}

		lock (_sync)
		{
			if (_latestPreviewFrame == null)
			{
				return null;
			}

			var widthLimit = Math.Max(1, maxWidth);
			var heightLimit = Math.Max(1, maxHeight);
			var scale = Math.Min((double)widthLimit / _latestPreviewFrame.Width, (double)heightLimit / _latestPreviewFrame.Height);
			scale = Math.Min(1.0, scale);

			var outWidth = Math.Max(1, (int)Math.Round(_latestPreviewFrame.Width * scale));
			var outHeight = Math.Max(1, (int)Math.Round(_latestPreviewFrame.Height * scale));

			var preview = new Bitmap(outWidth, outHeight);
			using var scaled = Graphics.FromImage(preview);
			scaled.DrawImage(_latestPreviewFrame, 0, 0, outWidth, outHeight);
			return preview;
		}
	}

	private async Task StartSessionAsync(ClipSession session)
	{
		try
		{
			await session.StartAsync();
			lock (_sync)
			{
				_session = session;
			}
		}
		catch
		{
			session.StatusChanged -= OnSessionStatusChanged;
			session.PreviewFrameReady -= OnPreviewFrameReady;
			session.Dispose();
			await StopAudioRecording();
			throw;
		}
	}

	public async Task StopCapture()
	{
		if (_disposed)
		{
			return;
		}

		ClipSession? session;
		lock (_sync)
		{
			session = _session;
			_session = null;
		}

		await StopAudioRecording();

		if (session != null)
		{
			await session.StopAsync();
			session.StatusChanged -= OnSessionStatusChanged;
			session.PreviewFrameReady -= OnPreviewFrameReady;
			session.Dispose();
		}

		lock (_sync)
		{
			_latestPreviewFrame?.Dispose();
			_latestPreviewFrame = null;
		}
	}

	public string GetSessionStatus()
	{
		lock (_sync)
		{
			return _lastSessionStatus;
		}
	}

	public string GetCaptureStats()
	{
		lock (_sync)
		{
			if (_session == null)
			{
				return "inactive";
			}

			return _session.GetDebugStatus();
		}
	}

	public string GetNvencReadiness()
	{
		var readiness = NvencHardwareEncoder.ProbeReadiness();
		return $"ready={readiness.IsReady};summary={readiness.Summary};maxVer=0x{readiness.MaxSupportedVersion:X8};cuda={readiness.CudaDriverVersion};fnPtrs={readiness.FunctionPointerCount}";
	}

	public Task StartAudioRecording(int clipSeconds)
	{
		ThrowIfDisposed();
		_micRecorder.StartMic(clipSeconds);
		_systemRecorder.StartSystem(clipSeconds);

		return Task.CompletedTask;
	}

	public Task StopAudioRecording()
	{
		_micRecorder.Stop();
		_systemRecorder.Stop();
		return Task.CompletedTask;
	}

	public async Task SaveClip()
	{
		ClipSession? session;
		lock (_sync)
		{
			session = _session;
		}

		if (session == null)
		{
			return;
		}

		var outputPath = GetDefaultClipPath();
		var tempVideoPath = GetTemporaryVideoPath(outputPath);
		await session.SaveClipAsync(tempVideoPath);
		if (!System.IO.File.Exists(tempVideoPath))
		{
			OnSessionStatusChanged("save_failed:no_video_file");
			return;
		}

		var (micPath, systemPath) = SaveAudioClip(outputPath);
		try
		{
			if (!string.IsNullOrWhiteSpace(micPath) || !string.IsNullOrWhiteSpace(systemPath))
			{
				await _mediaMuxer.MuxAsync(tempVideoPath, outputPath, micPath, systemPath);
				TryDeleteFile(tempVideoPath);
				TryDeleteFile(micPath);
				TryDeleteFile(systemPath);
				OnSessionStatusChanged($"save_ok:{outputPath}:muxed");
				return;
			}

			System.IO.File.Move(tempVideoPath, outputPath, overwrite: true);
			OnSessionStatusChanged($"save_ok:{outputPath}");
		}
		catch (Exception ex)
		{
			System.IO.File.Move(tempVideoPath, outputPath, overwrite: true);
			OnSessionStatusChanged($"save_partial:{outputPath}:audio_mux_failed:{ex.GetType().Name}");
		}
	}

	private (string? MicPath, string? SystemPath) SaveAudioClip(string outputPath)
	{
		var baseFolder = GetDefaultMediaFolder();
		var clipStem = System.IO.Path.GetFileNameWithoutExtension(outputPath);
		var micPath = System.IO.Path.Combine(baseFolder, $"{clipStem}.mic.wav");
		var systemPath = System.IO.Path.Combine(baseFolder, $"{clipStem}.system.wav");
		var savedMicPath = _micRecorder.SaveClip(micPath);
		var savedSystemPath = _systemRecorder.SaveClip(systemPath);
		return (savedMicPath, savedSystemPath);
	}

	private static RecordingSettings CreateDefaultSettings()
	{
		var appConfig = AppConfig.Load();
		var monitorIndex = ActiveTargetResolver.GetActiveMonitorIndex();
		var screens = Screen.AllScreens;
		var screen = (monitorIndex >= 0 && monitorIndex < screens.Length) ? screens[monitorIndex] : Screen.PrimaryScreen;
		var bounds = screen?.Bounds ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);

		return new RecordingSettings
		{
			Width = bounds.Width,
			Height = bounds.Height,
			Fps = 60,
			Bitrate = 12_000_000,
			ClipSeconds = 20,
			UseNativeNvenc = appConfig.NativeNvencEnabled,
			Target = new MonitorTarget { MonitorIndex = monitorIndex }
		};
	}

	private static string GetDefaultClipPath()
	{
		var folder = GetDefaultMediaFolder();

		var fileName = $"clip_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
		return System.IO.Path.Combine(folder, fileName);
	}

	private static string GetTemporaryVideoPath(string finalOutputPath)
	{
		var folder = System.IO.Path.GetDirectoryName(finalOutputPath) ?? GetDefaultMediaFolder();
		var stem = System.IO.Path.GetFileNameWithoutExtension(finalOutputPath);
		return System.IO.Path.Combine(folder, $"{stem}.{Guid.NewGuid():N}.video.tmp.mp4");
	}

	private static string GetDefaultMediaFolder()
	{
		var folder = System.IO.Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
			"inplayed");
		System.IO.Directory.CreateDirectory(folder);
		return folder;
	}

	private static void TryDeleteFile(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return;
		}

		try
		{
			if (System.IO.File.Exists(path))
			{
				System.IO.File.Delete(path);
			}
		}
		catch
		{
		}
	}

	private void OnSessionStatusChanged(string status)
	{
		lock (_sync)
		{
			_lastSessionStatus = status;
		}

		Console.WriteLine($"Session status: {status}");
	}

	private void OnPreviewFrameReady(Bitmap frame)
	{
		lock (_sync)
		{
			_latestPreviewFrame?.Dispose();
			_latestPreviewFrame = frame;
		}
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		try
		{
			StopCapture().GetAwaiter().GetResult();
		}
		catch
		{
		}

		_micRecorder.Dispose();
		_systemRecorder.Dispose();
		lock (_sync)
		{
			_latestPreviewFrame?.Dispose();
			_latestPreviewFrame = null;
		}
		_disposed = true;
	}

	private void ThrowIfDisposed()
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(CaptureController));
		}
	}
}
